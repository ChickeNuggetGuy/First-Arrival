using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class Chunk : Node3D
{
	[Export] public Enums.ChunkType chunkType;
	
    public int chunkSize;
    public float cellSize;
    public ChunkData chunkData;
    public Vector2I gridCoords;

    // --- Mesh Data ---
    private ArrayMesh mesh;
    public MeshInstance3D meshInstance;

    private Vector3[] localVertices;
    private List<int> triangles;
    private List<Vector2> uv;

    public Aabb bounds;

    public void Initialize(
        int chunkIndexX,
        int chunkIndexY,
        int chunkSize,
        Vector3[,] globalVertices,
        float cellSize,
        ChunkData chunkData
    )
    {
        this.gridCoords = new Vector2I(chunkIndexX, chunkIndexY);
        this.chunkSize = chunkSize;
        this.cellSize = cellSize;
        this.chunkData = chunkData;

        chunkData.chunk = this;
        
        if (chunkData.chunkType == ChunkData.ChunkType.ManMade)
        {
            GD.Print("Skipping mesh generation for ManMade chunk.");
            return;
        }

        meshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance");
        if (meshInstance == null)
        {
            meshInstance = new MeshInstance3D { Name = "MeshInstance" };
            AddChild(meshInstance);
        }
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        localVertices = new Vector3[(chunkSize + 1) * (chunkSize + 1)];
        int startX = chunkIndexX * chunkSize;
        int startY = chunkIndexY * chunkSize;

        int i = 0;
        for (int y = 0; y <= chunkSize; y++)
        {
	        for (int x = 0; x <= chunkSize; x++)
	        {
		        Vector3 worldPos = globalVertices[startX + x, startY + y];
		        float localX = worldPos.X - (startX * this.cellSize);
		        float localY = worldPos.Y;
		        float localZ = worldPos.Z - (startY * this.cellSize); 

		        localVertices[i++] = new Vector3(localX, localY, localZ);
	        }
        }
        
    }

    public void Generate(
        Material material,
        ShaderMaterial grassMaterial = null,
        int grassBladesPerCell = 0,
        int grassCardsPerClump = 3,
        int grassBladeSegments = 3,
        float grassBladeHeight = 0.55f,
        float grassBladeWidth = 0.08f,
        ISet<Vector2I> grassExcludedCells = null
    )
    {
        if (chunkData.chunkType == ChunkData.ChunkType.ManMade)
            return;

        mesh = new ArrayMesh();

        triangles = new List<int>();
        uv = new List<Vector2>();

        for (int y = 0; y <= chunkSize; y++)
        {
            for (int x = 0; x <= chunkSize; x++)
            {
                uv.Add(new Vector2((float)x / chunkSize, (float)y / chunkSize));
            }
        }

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int bottomLeftIndex = y * (chunkSize + 1) + x;
                int bottomRightIndex = y * (chunkSize + 1) + (x + 1);
                int topLeftIndex = (y + 1) * (chunkSize + 1) + x;
                int topRightIndex = (y + 1) * (chunkSize + 1) + (x + 1);

                // First triangle: BL -> BR -> TL
                triangles.Add(bottomLeftIndex);
                triangles.Add(bottomRightIndex);
                triangles.Add(topLeftIndex);

                // Second triangle: BR -> TR -> TL
                triangles.Add(bottomRightIndex);
                triangles.Add(topRightIndex);
                triangles.Add(topLeftIndex);
            }
        }

        var meshArrays = new Godot.Collections.Array();
        meshArrays.Resize((int)Mesh.ArrayType.Max);

        meshArrays[(int)Mesh.ArrayType.Vertex] = localVertices;
        meshArrays[(int)Mesh.ArrayType.Index] = triangles.ToArray();
        meshArrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArrays);

        CalculateSmoothNormals(mesh);

        meshInstance.Mesh = mesh;
        meshInstance.MaterialOverride = material;

        meshInstance.CreateTrimeshCollision();

        GenerateGrass(
            grassMaterial,
            grassBladesPerCell,
            grassCardsPerClump,
            grassBladeSegments,
            grassBladeHeight,
            grassBladeWidth,
            grassExcludedCells
        );

        if (meshInstance.GetChildCount() > 0)
        {
            var sb = meshInstance.GetChildOrNull<StaticBody3D>(0);
            if (sb != null)
            {
                sb.SetCollisionLayerValue(2, true);
                sb.SetCollisionMaskValue(2, true);
            }
        }

        bounds = mesh.GetAabb();

        this.AddToGroup("Mouse");
    }

    private void GenerateGrass(
        ShaderMaterial grassMaterial,
        int bladesPerCell,
        int cardsPerClump,
        int bladeSegments,
        float bladeHeight,
        float bladeWidth,
        ISet<Vector2I> excludedCells
    )
    {
        var existingGrass = GetNodeOrNull<MultiMeshInstance3D>("Grass");
        if (existingGrass != null)
        {
            RemoveChild(existingGrass);
            existingGrass.QueueFree();
        }

        if (grassMaterial == null || bladesPerCell <= 0 || localVertices == null)
            return;

        var grass = new MultiMeshInstance3D
        {
            Name = "Grass",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            ExtraCullMargin = bladeHeight
        };
        var bladeMesh = CreateGrassClumpMesh(
            bladeWidth,
            bladeHeight,
            Mathf.Max(2, cardsPerClump),
            Mathf.Max(2, bladeSegments),
            grassMaterial
        );

        int includedCellCount = 0;
        for (int z = 0; z < chunkSize; z++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                var globalCell = new Vector2I(
                    gridCoords.X * chunkSize + x,
                    gridCoords.Y * chunkSize + z
                );
                if (excludedCells == null || !excludedCells.Contains(globalCell))
                    includedCellCount++;
            }
        }

        int bladeCount = includedCellCount * bladesPerCell;
        if (bladeCount <= 0)
            return;

        float windMargin = Mathf.Max(bladeWidth * 2f, bladeHeight * 0.75f);
        float chunkWorldSize = chunkSize * cellSize;
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = bladeCount,
            Mesh = bladeMesh,
            CustomAabb = new Aabb(
                new Vector3(-windMargin, -0.05f, -windMargin),
                new Vector3(
                    chunkWorldSize + windMargin * 2f,
                    bladeHeight * 1.75f + 0.05f,
                    chunkWorldSize + windMargin * 2f
                )
            )
        };

        // A jittered grid fills each terrain cell more evenly than unrelated random
        // points. Hashing global cell coordinates prevents a visible repeated pattern
        // at every chunk boundary.
        int strataX = Mathf.CeilToInt(Mathf.Sqrt(bladesPerCell));
        int strataZ = Mathf.CeilToInt((float)bladesPerCell / strataX);
        int instance = 0;
        for (int z = 0; z < chunkSize; z++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int globalCellX = gridCoords.X * chunkSize + x;
                int globalCellZ = gridCoords.Y * chunkSize + z;
                if (
                    excludedCells != null
                    && excludedCells.Contains(new Vector2I(globalCellX, globalCellZ))
                )
                    continue;

                for (int blade = 0; blade < bladesPerCell; blade++)
                {
                    int stratumX = blade % strataX;
                    int stratumZ = blade / strataX;
                    float xJitter = Hash01(globalCellX, globalCellZ, blade, 17);
                    float zJitter = Hash01(globalCellX, globalCellZ, blade, 59);
                    float xFraction = (stratumX + xJitter) / strataX;
                    float zFraction = (stratumZ + zJitter) / strataZ;
                    float heightScale = Mathf.Lerp(
                        0.72f,
                        1.3f,
                        Hash01(globalCellX, globalCellZ, blade, 101)
                    );
                    float widthScale = Mathf.Lerp(
                        0.78f,
                        1.18f,
                        Hash01(globalCellX, globalCellZ, blade, 127)
                    );
                    float yaw =
                        Hash01(globalCellX, globalCellZ, blade, 149) * Mathf.Tau;

                    float height = SampleLocalHeight(x, z, xFraction, zFraction);
                    var position = new Vector3(
                        (x + xFraction) * cellSize,
                        height + 0.005f,
                        (z + zFraction) * cellSize
                    );

                    var scale = new Vector3(widthScale, heightScale, widthScale);
                    var basis = new Basis(Vector3.Up, yaw).Scaled(scale);
                    multiMesh.SetInstanceTransform(instance++, new Transform3D(basis, position));
                }
            }
        }

        grass.Multimesh = multiMesh;
        AddChild(grass);
    }

    private static ArrayMesh CreateGrassClumpMesh(
        float bladeWidth,
        float bladeHeight,
        int cardCount,
        int segmentCount,
        ShaderMaterial material
    )
    {
        int verticesPerCard = (segmentCount + 1) * 2;
        var vertices = new List<Vector3>(cardCount * verticesPerCard);
        var normals = new List<Vector3>(cardCount * verticesPerCard);
        var uvs = new List<Vector2>(cardCount * verticesPerCard);
        var colors = new List<Color>(cardCount * verticesPerCard);
        var indices = new List<int>(cardCount * segmentCount * 6);

        for (int card = 0; card < cardCount; card++)
        {
            // The golden angle keeps the cards from forming an obvious, uniform star.
            float angle = card * 2.3999632f;
            Vector3 forward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 right = new Vector3(forward.Z, 0f, -forward.X);

            float rootAngle = (card + 0.5f) * 3.883222f;
            float rootRadius = bladeWidth * 0.55f
                * Mathf.Sqrt((card + 0.5f) / cardCount);
            Vector3 center = new Vector3(
                Mathf.Cos(rootAngle),
                0f,
                Mathf.Sin(rootAngle)
            ) * rootRadius;

            float cardWidth = bladeWidth
                * Mathf.Lerp(0.82f, 1.12f, Hash01(card, cardCount, 0, 211));
            float cardHeight = bladeHeight
                * Mathf.Lerp(0.86f, 1.08f, Hash01(card, cardCount, 0, 263));
            Vector3 halfWidth = right * cardWidth * 0.5f;

            // Vertex color carries per-card shape data to the shader:
            // RG = local bend direction, B = curvature amount, A = wind phase.
            float curveAngle = angle + Mathf.Lerp(
                -0.45f,
                0.45f,
                Hash01(card, cardCount, 0, 307)
            );
            Vector3 curveDirection = new Vector3(
                Mathf.Sin(curveAngle),
                0f,
                Mathf.Cos(curveAngle)
            );
            if (Hash01(card, cardCount, 0, 331) < 0.5f)
                curveDirection = -curveDirection;

            var shapeData = new Color(
                curveDirection.X * 0.5f + 0.5f,
                curveDirection.Z * 0.5f + 0.5f,
                Hash01(card, cardCount, 0, 359),
                Hash01(card, cardCount, 0, 383)
            );
            int start = vertices.Count;

            for (int segment = 0; segment <= segmentCount; segment++)
            {
                float heightFraction = (float)segment / segmentCount;
                Vector3 rowCenter =
                    center + Vector3.Up * cardHeight * heightFraction;

                vertices.Add(rowCenter - halfWidth);
                vertices.Add(rowCenter + halfWidth);
                normals.Add(forward);
                normals.Add(forward);

                // UV.y is one at the ground and zero at the grass tip.
                float uvY = 1f - heightFraction;
                uvs.Add(new Vector2(0f, uvY));
                uvs.Add(new Vector2(1f, uvY));
                colors.Add(shapeData);
                colors.Add(shapeData);
            }

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int bottomLeft = start + segment * 2;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + 2;
                int topRight = bottomLeft + 3;

                indices.Add(bottomLeft);
                indices.Add(bottomRight);
                indices.Add(topRight);
                indices.Add(bottomLeft);
                indices.Add(topRight);
                indices.Add(topLeft);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private float SampleLocalHeight(int cellX, int cellZ, float xFraction, float zFraction)
    {
        int rowWidth = chunkSize + 1;
        int bottomLeft = cellZ * rowWidth + cellX;
        int bottomRight = bottomLeft + 1;
        int topLeft = (cellZ + 1) * rowWidth + cellX;
        int topRight = topLeft + 1;

        float bottomLeftHeight = localVertices[bottomLeft].Y;
        float bottomRightHeight = localVertices[bottomRight].Y;
        float topLeftHeight = localVertices[topLeft].Y;
        float topRightHeight = localVertices[topRight].Y;

        // Match the two triangles used by the terrain mesh. Bilinear interpolation
        // can otherwise leave grass floating above or buried in a sloped cell.
        if (xFraction + zFraction <= 1f)
        {
            return bottomLeftHeight
                + xFraction * (bottomRightHeight - bottomLeftHeight)
                + zFraction * (topLeftHeight - bottomLeftHeight);
        }

        float bottomRightWeight = 1f - zFraction;
        float topLeftWeight = 1f - xFraction;
        float topRightWeight = xFraction + zFraction - 1f;
        return bottomRightHeight * bottomRightWeight
            + topLeftHeight * topLeftWeight
            + topRightHeight * topRightWeight;
    }

    private static float Hash01(int x, int z, int blade, int salt)
    {
        float value = Mathf.Sin(x * 12.9898f + z * 78.233f + blade * 37.719f + salt * 0.123f)
            * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    // ----------------------------------
    // Normal/Tangent Utility Functions
    // ----------------------------------

    public static void CalculateSmoothNormals(ArrayMesh mesh)
    {
        var meshArrays = mesh.SurfaceGetArrays(0);
        Vector3[] vertices =
            (Vector3[])meshArrays[(int)Mesh.ArrayType.Vertex];
        int[] indices = (int[])meshArrays[(int)Mesh.ArrayType.Index];

        Vector3[] normals = new Vector3[vertices.Length];

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Vector3 edge1 = vertices[i1] - vertices[i0];
            Vector3 edge2 = vertices[i2] - vertices[i0];

            Vector3 faceNormal = edge2.Cross(edge1);
            if (faceNormal.LengthSquared() < Mathf.Epsilon)
                continue;

            normals[i0] += faceNormal;
            normals[i1] += faceNormal;
            normals[i2] += faceNormal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            if (normals[i].LengthSquared() > Mathf.Epsilon)
                normals[i] = normals[i].Normalized();
            else
                normals[i] = Vector3.Up;
        }

        meshArrays[(int)Mesh.ArrayType.Normal] = normals;
        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArrays);
    }

    public static void RecalculateMeshNormalsInPlace(MeshInstance3D meshInstance)
    {
        ArrayMesh mesh = meshInstance.Mesh as ArrayMesh;
        if (mesh == null)
            return;

        var meshArrays = mesh.SurfaceGetArrays(0);
        Vector3[] vertices =
            (Vector3[])meshArrays[(int)Mesh.ArrayType.Vertex];
        int[] indices = (int[])meshArrays[(int)Mesh.ArrayType.Index];

        Vector3[] normals = new Vector3[vertices.Length];

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Vector3 edge1 = vertices[i1] - vertices[i0];
            Vector3 edge2 = vertices[i2] - vertices[i0];
            Vector3 faceNormal = edge2.Cross(edge1);

            float area = faceNormal.Length();
            if (area < Mathf.Epsilon)
                continue;

            faceNormal /= area;

            normals[i0] += faceNormal;
            normals[i1] += faceNormal;
            normals[i2] += faceNormal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            if (normals[i].LengthSquared() > Mathf.Epsilon)
                normals[i] = normals[i].Normalized();
            else
                normals[i] = Vector3.Up;
        }

        meshArrays[(int)Mesh.ArrayType.Normal] = normals;
        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArrays);
    }

    public static void CalculateFlatNormals(ArrayMesh mesh)
    {
        var meshArrays = mesh.SurfaceGetArrays(0);
        Vector3[] vertices =
            (Vector3[])meshArrays[(int)Mesh.ArrayType.Vertex];
        int[] indices = (int[])meshArrays[(int)Mesh.ArrayType.Index];

        Vector3[] normals = new Vector3[vertices.Length];

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            Vector3 faceNormal = (v2 - v0).Cross(v1 - v0);
            if (faceNormal.LengthSquared() < Mathf.Epsilon)
                faceNormal = Vector3.Up;
            else
                faceNormal = faceNormal.Normalized();

            normals[i0] = faceNormal;
            normals[i1] = faceNormal;
            normals[i2] = faceNormal;
        }

        meshArrays[(int)Mesh.ArrayType.Normal] = normals;
        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArrays);
    }
}
