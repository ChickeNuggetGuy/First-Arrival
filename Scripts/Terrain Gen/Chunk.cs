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
		int grassCardsPerClump = 5,
        float grassBladeHeight = 0.45f,
        float grassBladeWidth = 0.09f
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
            grassBladeHeight,
            grassBladeWidth
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
        float bladeHeight,
        float bladeWidth
    )
    {
        var existingGrass = GetNodeOrNull<MultiMeshInstance3D>("Grass");
        existingGrass?.QueueFree();

        if (grassMaterial == null || bladesPerCell <= 0 || localVertices == null)
            return;

        var grass = new MultiMeshInstance3D { Name = "Grass" };
        var bladeMesh = CreateGrassClumpMesh(
            bladeWidth,
            bladeHeight,
            Mathf.Max(2, cardsPerClump),
            grassMaterial
        );

        int bladeCount = chunkSize * chunkSize * bladesPerCell;
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = bladeCount,
            Mesh = bladeMesh,
            CustomAabb = new Aabb(
                Vector3.Zero,
                new Vector3(chunkSize * cellSize, bladeHeight * 1.5f, chunkSize * cellSize)
            )
        };

        int instance = 0;
        for (int z = 0; z < chunkSize; z++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                for (int blade = 0; blade < bladesPerCell; blade++)
                {
                    float xFraction = Hash01(x, z, blade, 17);
                    float zFraction = Hash01(x, z, blade, 59);
                    float scale = Mathf.Lerp(0.7f, 1.25f, Hash01(x, z, blade, 101));
                    float yaw = Hash01(x, z, blade, 149) * Mathf.Tau;

                    float height = SampleLocalHeight(x, z, xFraction, zFraction);
                    var position = new Vector3(
                        (x + xFraction) * cellSize,
						height + 0.01f,
                        (z + zFraction) * cellSize
                    );

                    var basis = new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale);
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
        ShaderMaterial material
    )
    {
        var vertices = new List<Vector3>(cardCount * 4);
        var normals = new List<Vector3>(cardCount * 4);
        var uvs = new List<Vector2>(cardCount * 4);
        var indices = new List<int>(cardCount * 6);

        for (int card = 0; card < cardCount; card++)
        {
            float angle = card * Mathf.Tau / cardCount;
            Vector3 forward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 right = new Vector3(forward.Z, 0f, -forward.X);

            // Offset the cards slightly so each instance reads as a clump,
            // rather than several identical blades occupying one plane.
            Vector3 center = forward * bladeWidth * (card % 2 == 0 ? 0.3f : 0.6f);
            Vector3 halfWidth = right * bladeWidth * 0.5f;
            int start = vertices.Count;

            vertices.Add(center - halfWidth);
            vertices.Add(center + halfWidth);
            vertices.Add(center + halfWidth + Vector3.Up * bladeHeight);
            vertices.Add(center - halfWidth + Vector3.Up * bladeHeight);

            normals.Add(forward);
            normals.Add(forward);
            normals.Add(forward);
            normals.Add(forward);

            // UV.y is one at the ground and zero at the grass tip.
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 0f));

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
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

        float bottom = Mathf.Lerp(localVertices[bottomLeft].Y, localVertices[bottomRight].Y, xFraction);
        float top = Mathf.Lerp(localVertices[topLeft].Y, localVertices[topRight].Y, xFraction);
        return Mathf.Lerp(bottom, top, zFraction);
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
