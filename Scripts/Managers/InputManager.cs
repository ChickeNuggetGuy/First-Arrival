using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class InputManager : Manager<InputManager>
{
	private GridSystem gridSystem;
	public GridCell currentGridCell { get; protected set; }
	public HexCellData? CurrentCell { get; private set; }
	[Export] public Camera3D camera3D;
	[Export] private Node3D mouseMarker;

	[Export] public CollisionObject3D globeMesh;


	public bool MouseOverUI
	{
		get => IsMouseOverUI();
		private set { }
	}

	public override string GetManagerName() => "InputManager";

	protected override Task _Setup(bool loadingData)
	{
		gridSystem = GridSystem.Instance;
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		return Task.CompletedTask;
	}

	private bool IsMouseOverUI() => GetViewport()?.GuiGetHoveredControl() != null;


	public override void _Process(double delta)
	{
		if (UIManager.Instance.BlockingInput) return;
		base._Process(delta);
		if (!ExecuteComplete) return;
		WorldMouseMarker();
		if (DebugMode && currentGridCell != null)
		{
			GD.Print(
				$"Mouse position: {currentGridCell.GridCoordinates} {gridSystem.HasConnections(currentGridCell.GridCoordinates)}");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		return;
	}

	
	private void WorldMouseMarker()
	{
		if (MouseOverUI) return;

		if (GameManager.Instance.currentScene == GameManager.GameScene.BattleScene)
		{
			Node node = GetObjectAtMousePosition(out Vector3 hitPosition) as Node;

			if (node == null) return;

			GridObject gridObject = null;
			int maxParentChecks = 3;
			int parentLevel = 0;

			// Traverse up to 3 parents to find a GridObject
			Node currentNode = node;
			if (node.IsInGroup("GridObjects"))
			{
				while (currentNode != null && parentLevel <= maxParentChecks)
				{
					if (currentNode is GridObject go)
					{
						gridObject = go;
						break;
					}

					currentNode = currentNode.GetParent();
					parentLevel++;
				}
			}

			if (gridObject != null)
			{
				var cell = gridObject.GridPositionData?.AnchorCell;

				if (cell != null)
				{
					currentGridCell = cell;
					if (mouseMarker != null)
						mouseMarker.GlobalPosition = cell.GridCoordinates * new Vector3I(1, 1, 1);
					return;
				}
				else
				{
					GD.PrintErr(
						$"GridObject '{gridObject?.Name}' has null GridPositionData or GridCell."
					);
				}
			}

			// Fall back to position-based lookup
			if (gridSystem != null
			    && gridSystem.TryGetGridCellFromWorldPosition(
				    hitPosition,
				    out GridCell gridCell,
				    true
			    )
			    && gridCell != null)
			{
				if (gridCell.state.HasFlag(Enums.GridCellState.Air))
				{
					gridCell = gridSystem.GetGridCell(
						gridCell.GridCoordinates - new Vector3I(0, 1, 0)
					);
				}

				if (gridCell != null)
				{
					currentGridCell = gridCell;
					if (mouseMarker != null)
						mouseMarker.GlobalPosition =
							gridCell.GridCoordinates * new Vector3I(1, 1, 1);
				}
			}
			else
			{
				currentGridCell = GridCell.Null;
				if (mouseMarker != null)
					mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
			}
		}
		else if (GameManager.Instance.currentScene == GameManager.GameScene.GlobeScene)
		{
			Vector3? mousePos = GetMouseGlobePosition();

			if (mousePos != null)
			{
				HexCellData? cell =
					GlobeHexGridManager.Instance.GetCellFromPosition(mousePos.Value);

				if (cell != null)
				{
					CurrentCell = cell;
					GlobeHexGridManager.Instance.SetDebugHighlightedCountryFromIndex(
						cell.Value.Index
					);

					if (mouseMarker != null)
						mouseMarker.GlobalPosition = cell.Value.Center;

					return;
				}
			}

			// No hit / no cell: clear hover + highlight
			CurrentCell = null;
			GlobeHexGridManager.Instance.SetDebugHighlightedCountryFromIndex(-1);

			if (mouseMarker != null)
				mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
		}
	}

	public GodotObject GetObjectAtMousePosition(out Vector3 worldPosition)
	{
		worldPosition = new Vector3(-1, -1, -1);
		if (IsMouseOverUI())
		{
			//Object will be a UI Object
			return GetViewport()?.GuiGetHoveredControl();
		}
		else
		{
			var space = GetTree().Root.GetWorld3D().DirectSpaceState;
			var mousePosition = GetViewport().GetMousePosition();

			var cam = camera3D;
			var from = cam.ProjectRayOrigin(mousePosition);
			var to = from + cam.ProjectRayNormal(mousePosition) * cam.Far;

			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.CollideWithAreas = true;
			query.CollideWithBodies = true;
			query.CollisionMask = PhysicsLayer.TERRAIN | PhysicsLayer.GRIDOBJECT;

			var result = space.IntersectRay(query);

			if (result.Count == 0)
			{
				currentGridCell = GridCell.Null;
				if (mouseMarker != null)
					mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
				return null;
			}

			worldPosition = result["position"].As<Vector3>();
			var colliderObject = result["collider"].As<GodotObject>();
			Node node = colliderObject as Node;

			// Recursively check for GridObject parent
			GridObject gridObject = null;
			Node current = node;
			while (current != null)
			{
				if (current is GridObject go)
				{
					gridObject = go;
					break;
				}

				current = current.GetParent();
			}

			if (gridObject != null)
			{
				currentGridCell = gridObject.GridPositionData?.AnchorCell;
				if (mouseMarker != null && currentGridCell != null)
					mouseMarker.GlobalPosition = currentGridCell.WorldCenter;
				return gridObject;
			}


			return node;
		}
	}


	#region Globe Input Functions

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

	#endregion

	#region manager Data

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		base.Load(data);
		if (!HasLoadedData) return;
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