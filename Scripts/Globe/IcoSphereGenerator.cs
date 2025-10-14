using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class IcoSphereGenerator : MeshInstance3D
{
  [Export(PropertyHint.Range, "0,5,1")]
  public int Subdivisions { get; set; } = 2;

  [Export]
  public float Radius { get; set; } = 1.0f;

  // Regular icosahedron (normalized)
  private static readonly Vector3[] BaseVerts =
  {
    new(0.8506508f, 0.5257311f, 0f),
    new(0f, 0.8506508f, -0.5257311f),
    new(0f, 0.8506508f, 0.5257311f),
    new(0.5257311f, 0f, -0.8506508f),
    new(0.5257311f, 0f, 0.8506508f),
    new(0.8506508f, -0.5257311f, 0f),
    new(-0.5257311f, 0f, -0.8506508f),
    new(-0.8506508f, 0.5257311f, 0f),
    new(-0.5257311f, 0f, 0.8506508f),
    new(0f, -0.8506508f, -0.5257311f),
    new(0f, -0.8506508f, 0.5257311f),
    new(-0.8506508f, -0.5257311f, 0f),
  };

  private static readonly int[] BaseTris =
  {
    0, 1, 2,
    0, 3, 1,
    0, 2, 4,
    3, 0, 5,
    0, 4, 5,
    1, 3, 6,
    1, 7, 2,
    7, 1, 6,
    4, 2, 8,
    7, 8, 2,
    9, 3, 5,
    6, 3, 9,
    5, 4, 10,
    4, 8, 10,
    9, 5, 10,
    7, 6, 11,
    7, 11, 8,
    11, 6, 9,
    8, 11, 10,
    10, 11, 9,
  };

  public override void _Ready()
  {
    BuildMesh();
  }

  private void BuildMesh()
  {
    var vertices = new List<Vector3>(BaseVerts.Length);
    for (int i = 0; i < BaseVerts.Length; i++)
      vertices.Add(BaseVerts[i].Normalized());

    var triangles = new List<int>(BaseTris);

    // Subdivide: each triangle -> 4 triangles, reuse edge midpoints
    for (int s = 0; s < Subdivisions; s++)
    {
      var newTris = new List<int>(triangles.Count * 4);
      var midpointCache = new Dictionary<long, int>(triangles.Count);

      for (int t = 0; t < triangles.Count; t += 3)
      {
        int a = triangles[t];
        int b = triangles[t + 1];
        int c = triangles[t + 2];

        int ab = GetMidpointIndex(a, b, vertices, midpointCache);
        int bc = GetMidpointIndex(b, c, vertices, midpointCache);
        int ca = GetMidpointIndex(c, a, vertices, midpointCache);
        
        newTris.AddRange(new int[]
        {
	        a,  ca, ab,
	        b,  ab, bc,
	        c,  bc, ca,
	        ab, ca, bc,
        });
      }

      triangles = newTris;
    }

    // Project to sphere radius
    for (int i = 0; i < vertices.Count; i++)
      vertices[i] = vertices[i].Normalized() * Radius;

    // Normals are just the normalized positions on a sphere
    var normals = new List<Vector3>(vertices.Count);
    for (int i = 0; i < vertices.Count; i++)
      normals.Add(vertices[i].Normalized());

    // Simple spherical UVs (will have a seam at U ~ 0/1)
    var uvs = new List<Vector2>(vertices.Count);
    for (int i = 0; i < vertices.Count; i++)
      uvs.Add(SphericalUV(vertices[i]));

    // Build surface
    var surfaceArray = new Godot.Collections.Array();
    surfaceArray.Resize((int)Mesh.ArrayType.Max);
    surfaceArray[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
    surfaceArray[(int)Mesh.ArrayType.Normal] = normals.ToArray();
    surfaceArray[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
    surfaceArray[(int)Mesh.ArrayType.Index] = triangles.ToArray();

    var arrMesh = new ArrayMesh();
    arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
    Mesh = arrMesh;

    MaterialOverride = new StandardMaterial3D()
    {
	    AlbedoColor = Colors.Blue
    };
  }

  private static int GetMidpointIndex(
    int i1,
    int i2,
    List<Vector3> verts,
    Dictionary<long, int> cache
  )
  {
    // Unordered edge key: (min,max) packed into a 64-bit integer
    long key = i1 < i2
      ? (((long)i1) << 32) | (uint)i2
      : (((long)i2) << 32) | (uint)i1;

    if (cache.TryGetValue(key, out int idx))
      return idx;

    Vector3 mid = (verts[i1] + verts[i2]) * 0.5f;
    int newIndex = verts.Count;
    verts.Add(mid.Normalized()); // keep on unit sphere; scale later
    cache[key] = newIndex;
    return newIndex;
  }

  private static Vector2 SphericalUV(Vector3 p)
  {
    Vector3 n = p.Normalized();
    float u = Mathf.Atan2(n.Z, n.X) / (Mathf.Pi * 2.0f) + 0.5f;
    float v = Mathf.Acos(Mathf.Clamp(n.Y, -1f, 1f)) / Mathf.Pi;
    return new Vector2(u, v);
  }
}