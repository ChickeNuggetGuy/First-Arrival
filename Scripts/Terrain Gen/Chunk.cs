using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class Chunk : Node3D
{
    public int chunkSize;
    public float cellSize;
    public ChunkData chunkData;
    public Vector2I gridCoords;

    // --- Mesh Data ---
    private ArrayMesh mesh;
    public MeshInstance3D meshInstance;

    // We store local vertices separately so they start at (0,0,0) in local space
    private Vector3[] localVertices;
    private List<int> triangles;
    private List<Vector2> uv;

    // This keeps track of the bounding volume of the final chunk mesh
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

	    // Keep the wrapper node reference unchanged; only link the component
	    chunkData.chunk = this;

	    GD.Print(
		    $"Initializing chunk at {gridCoords}, Type: {chunkData.chunkType}"
	    );

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

	    GD.Print(
		    $"Chunk {gridCoords} initialized with {localVertices.Length} vertices."
	    );
    }

    public void Generate(Color color)
    {
        if (chunkData.chunkType == ChunkData.ChunkType.ManMade)
            return;

        mesh = new ArrayMesh();

        List<Vector3> meshVerts = new List<Vector3>();
        triangles = new List<int>();
        uv = new List<Vector2>();

        // Build two triangles per cell in local space
        // localVertices is indexed row-major: idx = y*(chunkSize+1) + x
        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int bottomLeftIndex = (y) * (chunkSize + 1) + x;
                int bottomRightIndex = (y) * (chunkSize + 1) + (x + 1);
                int topLeftIndex = (y + 1) * (chunkSize + 1) + x;
                int topRightIndex = (y + 1) * (chunkSize + 1) + (x + 1);

                Vector3 bottomLeft = localVertices[bottomLeftIndex];
                Vector3 bottomRight = localVertices[bottomRightIndex];
                Vector3 topLeft = localVertices[topLeftIndex];
                Vector3 topRight = localVertices[topRightIndex];

                // Clockwise winding for Y-up
                int v0 = meshVerts.Count;
                meshVerts.Add(bottomLeft);
                meshVerts.Add(bottomRight);
                meshVerts.Add(topLeft);
                triangles.Add(v0);
                triangles.Add(v0 + 1);
                triangles.Add(v0 + 2);

                int v1 = meshVerts.Count;
                meshVerts.Add(bottomRight);
                meshVerts.Add(topRight);
                meshVerts.Add(topLeft);
                triangles.Add(v1);
                triangles.Add(v1 + 1);
                triangles.Add(v1 + 2);

                // UVs (0..1 per chunk)
                uv.Add(new Vector2((float)x / chunkSize, (float)y / chunkSize));
                uv.Add(
                    new Vector2(
                        (float)(x + 1) / chunkSize,
                        (float)y / chunkSize
                    )
                );
                uv.Add(
                    new Vector2(
                        (float)x / chunkSize,
                        (float)(y + 1) / chunkSize
                    )
                );

                uv.Add(
                    new Vector2(
                        (float)(x + 1) / chunkSize,
                        (float)y / chunkSize
                    )
                );
                uv.Add(
                    new Vector2(
                        (float)(x + 1) / chunkSize,
                        (float)(y + 1) / chunkSize
                    )
                );
                uv.Add(
                    new Vector2(
                        (float)x / chunkSize,
                        (float)(y + 1) / chunkSize
                    )
                );
            }
        }

        var meshArrays = new Godot.Collections.Array();
        meshArrays.Resize((int)Mesh.ArrayType.Max);

        meshArrays[(int)Mesh.ArrayType.Vertex] = meshVerts.ToArray();
        meshArrays[(int)Mesh.ArrayType.Index] = triangles.ToArray();
        meshArrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArrays);

        CalculateSmoothNormals(mesh);

        meshInstance.Mesh = mesh;

        var newMaterial = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            CullMode = BaseMaterial3D.CullModeEnum.Back
        };
        meshInstance.MaterialOverride = newMaterial;

        meshInstance.CreateTrimeshCollision();
        // Set collision layer/mask safely if StaticBody was created
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
            Vector3 faceNormal = edge1.Cross(edge2);

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

            Vector3 faceNormal = (v1 - v0).Cross(v2 - v0);
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