using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot.Collections;

[GlobalClass]
public partial class GlobeInputManager : Manager<GlobeInputManager>
{
	[Export] public Camera3D camera3D;
	[Export] public CollisionObject3D globeMesh;
	[Export] public MeshInstance3D mouseMarker;
	public HexCellData? CurrentCell {get; private set;}
	


	public override void _PhysicsProcess(double delta)
	{
		Vector3? mousePos = GetMouseGlobePosition();

		if (mousePos != null)
		{
			// Vector2 latLon = GetLatLonFromPosition(mousePos.Value);

			HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromPosition(mousePos.Value);

			if (cell != null)
			{
				CurrentCell = cell;
			}
		}
		else
		{
		}

		if (CurrentCell != null)
		{
			mouseMarker.GlobalPosition = CurrentCell.Value.Center;
		}
	}

	public override string GetManagerName() => "GlobeInputManager";

	protected override async Task _Setup(bool loadingData)
	{
		return;
		throw new NotImplementedException();
	}

	protected override async Task _Execute(bool loadingData)
	{
		return;
	}

	public override Dictionary<string, Variant> Save()
	{
		return new Dictionary<string, Variant>();
	}

	public override void Load(Dictionary<string, Variant> data)
	{
		return;
	}

	public Vector3? GetMouseGlobePosition()
	{
		var spaceState = GetTree().Root.GetWorld3D().DirectSpaceState;

		var cam = camera3D;
		var mousePos = GetViewport().GetMousePosition();

		var origin = cam.ProjectRayOrigin(mousePos);
		var end = origin + cam.ProjectRayNormal(mousePos) * 400;
		var query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollideWithAreas = true;

		var result = spaceState.IntersectRay(query);

		if (result.Count == 0)
		{
			return null;
		}
		else if (result["collider"].AsGodotObject() == globeMesh)
		{
			return result["position"].AsVector3();
		}
		return null;
	}
	
	
	public Vector2 GetLatLonFromPosition(Vector3 position)
	{
		float radius = position.Length();
    
		// Normalize coordinates for the trig functions
		if (radius == 0) return Vector2.Zero;

		// 2. Calculate Latitude
		// Range: -PI/2 to PI/2 (-90 to +90 degrees)
		float latitude = Mathf.Asin(position.Y / radius);

		// 3. Calculate Longitude
		// Range: -PI to PI (-180 to +180 degrees)
		float longitude = Mathf.Atan2(position.X, position.Z);

		// Return as Radians (or convert to degrees if preferred)
		
		return new Vector2(Mathf.RadToDeg(latitude), Mathf.RadToDeg(longitude));
	}
	
	public override void Deinitialize()
	{
		return;
	}

	
}
