using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

/// <summary>
/// Handles mouse input while in the BattleScene: resolves which GridCell / GridObject
/// is under the cursor and keeps the world mouse marker in sync.
/// </summary>
[GlobalClass]
public partial class BattleInputManager : Manager<BattleInputManager>
{
	private GridSystem gridSystem;
	public GridCell currentGridCell { get; private set; }

	[Export] public Camera3D camera3D;
	[Export] private Node3D mouseMarker;

	public bool MouseOverUI => GetViewport()?.GuiGetHoveredControl() != null;

	public override string GetManagerName() => "BattleInputManager";

	protected override Task _Setup(bool loadingData)
	{
		gridSystem = GridSystem.Instance;
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
			if (DebugMode) GD.Print("[BattleInputManager] Blocked: UIManager.BlockingInput is true");
			return;
		}

		if (!ExecuteComplete)
		{
			GD.Print("Execute not Complete");
			return;
		}

		if (GameManager.Instance.currentScene != GameManager.GameScene.BattleScene)
		{
			if (DebugMode) GD.Print($"[BattleInputManager] Blocked: currentScene is {GameManager.Instance.currentScene}, not BattleScene");
			return;
		}

		if (DebugMode && gridSystem == null)
		{
			GD.Print("[BattleInputManager] gridSystem is null - was GridSystem.Instance set before this manager's _Setup ran?");
		}

		if (DebugMode && camera3D == null)
		{
			GD.Print("[BattleInputManager] camera3D export is not assigned in the scene");
		}

		UpdateGridCellUnderMouse();
		base._Process(delta);

		if (DebugMode)
		{
			GD.Print($"[BattleInputManager] currentGridCell = {(currentGridCell != null ? currentGridCell.GridCoordinates.ToString() : "null")}");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		return;
	}

	private void UpdateGridCellUnderMouse()
	{
		if (MouseOverUI)
		{
			if (DebugMode) GD.Print("[BattleInputManager] MouseOverUI is true, skipping raycast");
			return;
		}

		GodotObject hitObject = GetObjectAtMousePosition(out Vector3 hitPosition);
		
		if (hitObject == null)
		{
			if (DebugMode) GD.Print("[BattleInputManager] Raycast hit nothing (check camera3D, CollisionMask, and that terrain/GridObjects are on PhysicsLayer.TERRAIN/GRIDOBJECT)");
			ClearCurrentCell();
			return;
		}

		if (hitObject is GridObject gridObject)
		{
			var cell = gridObject.GridPositionData?.AnchorCell;

			if (cell != null)
			{
				SetCurrentCell(cell);
				return;
			}

			GD.PrintErr($"GridObject '{gridObject.Name}' has null GridPositionData or GridCell.");
		}

		// Fall back to position based lookup (e.g. bare terrain hit)
		if (gridSystem != null
		    && gridSystem.TryGetGridCellFromWorldPosition(hitPosition, out GridCell gridCell, true)
		    && gridCell != null)
		{
			if (gridCell.state.HasFlag(Enums.GridCellState.Air))
			{
				gridCell = gridSystem.GetGridCell(gridCell.GridCoordinates - new Vector3I(0, 1, 0));
			}

			if (gridCell != null)
			{
				SetCurrentCell(gridCell);
				return;
			}
		}

		if (DebugMode) GD.Print($"[BattleInputManager] Fallback lookup failed at hitPosition {hitPosition} (gridSystem null? {gridSystem == null})");
		ClearCurrentCell();
	}

	private void SetCurrentCell(GridCell cell)
	{
		currentGridCell = cell;
		if (mouseMarker != null)
			mouseMarker.GlobalPosition = cell.WorldCenter;
	}

	private void ClearCurrentCell()
	{
		currentGridCell = GridCell.Null;
		if (mouseMarker != null)
			mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
	}

	/// <summary>
	/// Raycasts from the camera through the mouse position and, if the hit collider
	/// belongs to a GridObject (at any parent depth), returns that GridObject instead
	/// of the raw collider node. Returns null on a total miss.
	/// </summary>
	public GodotObject GetObjectAtMousePosition(out Vector3 worldPosition)
	{
		worldPosition = new Vector3(-1, -1, -1);

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
			return null;
		}

		worldPosition = result["position"].As<Vector3>();
		var colliderObject = result["collider"].As<GodotObject>();
		Node node = colliderObject as Node;

		// Walk up parents to find an owning GridObject. Colliders are almost always on
		// a child node (CollisionShape3D/StaticBody3D), so we don't gate this on the
		// hit node itself being group-tagged - only the root GridObject usually is.
		Node current = node;
		while (current != null)
		{
			if (current is GridObject go)
			{
				return go;
			}

			current = current.GetParent();
		}

		return node;
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