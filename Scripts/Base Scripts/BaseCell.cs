using Godot;
using System;

[GlobalClass]
public partial class BaseCell : Node3D
{
	public Vector2 gridCoords;
	public GridShape shape;
	public Vector3 worldPosition;
	public MeshInstance3D meshInstance;
	[Export] public FacilityDefinition FacilityDefinition { get; private set; }
	public FacilityConstruction Construction { get; private set; }
	public bool IsFacilityOrigin { get; private set; }
	public string FacilityName => Construction?.DisplayName ?? string.Empty;

	public bool HasFacility => Construction != null;
	public bool HasConstructedFacility => Construction?.IsConstructed == true;

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

	public void ConfigureFacility(
		FacilityConstruction construction,
		bool isFacilityOrigin)
	{
		Construction = construction;
		IsFacilityOrigin = isFacilityOrigin;
		RefreshConstructionAppearance();
	}

	public void RefreshConstructionAppearance()
	{
		if (meshInstance == null) return;

		if (Construction == null)
		{
			meshInstance.Visible = true;
			meshInstance.Transparency = 0.0f;
			return;
		}

		meshInstance.Visible = IsFacilityOrigin;
		meshInstance.Transparency =
			Construction.IsConstructed ? 0.0f : 0.45f;
	}
}
