using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

/// <summary>
/// Handles mouse input while in the GlobeScene: resolves which HexCellData
/// is under the cursor on the globe mesh and keeps the world mouse marker in sync.
/// </summary>
[GlobalClass]
public partial class GlobeInputManager : Manager<GlobeInputManager>
{
	public HexCellData? CurrentCell { get; private set; }

	[Export] public Camera3D camera3D;
	[Export] private Node3D mouseMarker;
	[Export] public CollisionObject3D globeMesh;

	public bool MouseOverUI => GetViewport()?.GuiGetHoveredControl() != null;

	public override string GetManagerName() => "GlobeInputManager";

	protected override Task _Setup(bool loadingData)
	{
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		return Task.CompletedTask;
	}

	public override void _Process(double delta)
	{
		if (UIManager.Instance.BlockingInput)
		{
			return;
		}

		if (!ExecuteComplete)
		{
			return;
		}

		if (GameManager.Instance.currentScene != GameManager.GameScene.GlobeScene)
		{
			return;
		}

		UpdateHexCellUnderMouse();
		base._Process(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		return;
	}

	private void UpdateHexCellUnderMouse()
	{
		if (MouseOverUI)
		{
			ClearCurrentCell();
			return;
		}

		Vector3? mousePos = GetMouseGlobePosition();

		if (mousePos != null)
		{
			HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromPosition(mousePos.Value);

			if (cell != null)
			{
				CurrentCell = cell;
				GlobeHexGridManager.Instance.SetDebugHighlightedCountryFromIndex(cell.Value.Index);

				if (mouseMarker != null)
					mouseMarker.GlobalPosition = cell.Value.Center;

				return;
			}
		}

		ClearCurrentCell();
	}

	private void ClearCurrentCell()
	{
		CurrentCell = null;
		GlobeHexGridManager.Instance?.SetDebugHighlightedCountryFromIndex(-1);

		if (mouseMarker != null)
			mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
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

		if (radius == 0) return Vector2.Zero;

		float latitude = Mathf.Asin(position.Y / radius);
		float longitude = Mathf.Atan2(position.X, position.Z);

		return new Vector2(Mathf.RadToDeg(latitude), Mathf.RadToDeg(longitude));
	}

	#region manager Data

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!HasLoadedData) return Task.CompletedTask;
		return Task.CompletedTask;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		return null;
	}

	#endregion

	public override void Deinitialize()
	{
		return;
	}
}