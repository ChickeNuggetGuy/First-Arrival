using Godot;
using System;
using System.Collections.Generic;

public partial class SphereDrawer : MeshInstance3D
{
	private int _rings = 50;
	private int _radialSegments = 50;
	private float _radius = 1;
	public override void _Ready()
	{
		Godot.Collections.Array surfaceArray = [];
		surfaceArray.Resize((int)Mesh.ArrayType.Max);

		// C# arrays cannot be resized or expanded, so use Lists to create geometry.
		List<Vector3> verts = [];
		List<Vector2> uvs = [];
		List<Vector3> normals = [];
		List<int> indices = [];
		// Vertex indices.
		var thisRow = 0;
		var prevRow = 0;
		var point = 0;

		// Loop over rings.
		for (var i = 0; i < _rings + 1; i++)
		{
			var v = ((float)i) / _rings;
			var w = Mathf.Sin(Mathf.Pi * v);
			var y = Mathf.Cos(Mathf.Pi * v);

			// Loop over segments in ring.
			for (var j = 0; j < _radialSegments + 1; j++)
			{
				var u = ((float)j) / _radialSegments;
				var x = Mathf.Sin(u * Mathf.Pi * 2);
				var z = Mathf.Cos(u * Mathf.Pi * 2);
				var vert = new Vector3(x * _radius * w, y * _radius, z * _radius * w);
				verts.Add(vert);
				normals.Add(vert.Normalized());
				uvs.Add(new Vector2(u, v));
				point += 1;

				// Create triangles in ring using indices.
				if (i > 0 && j > 0)
				{
					indices.Add(prevRow + j - 1);
					indices.Add(prevRow + j);
					indices.Add(thisRow + j - 1);

					indices.Add(prevRow + j);
					indices.Add(thisRow + j);
					indices.Add(thisRow + j - 1);
				}
			}

			prevRow = thisRow;
			thisRow = point;
		}

		// Insert committing to the ArrayMesh here.
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
