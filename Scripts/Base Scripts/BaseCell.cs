using Godot;
using System;

[GlobalClass]
public partial class BaseCell : Node3D
{
	public Vector2 gridCoords;
	public Vector3 worldPosition;
	public MeshInstance3D meshInstance;

	public BaseCell()
	{
		gridCoords = Vector2.Zero;
		worldPosition = Vector3.Zero;
		meshInstance = null;
	}

	public BaseCell(int x, int z, Vector3 worldPosition, Mesh mesh)
	{
		gridCoords = new Vector2(x, z);
		this.worldPosition = worldPosition;
		this.Position = worldPosition;
		meshInstance = new MeshInstance3D();
		AddChild(meshInstance);
		meshInstance.Mesh = mesh;
	}
	
}
