using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class InputManager : Manager<InputManager>
{
	private GridSystem gridSystem;
	public GridCell currentGridCell
	{
		get;
		protected set;
	}
	
	[Export] private Node3D mouseMarker;
	public bool MouseOverUI
	{
		get => IsMouseOverUI(); private set{}}

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
		if(UIManager.Instance.BlockingInput) return;
		base._Process(delta);
		if( !ExecuteComplete) return;
		WorldMouseMarker();
		if (DebugMode && currentGridCell != null)
		{
			GD.Print($"Mouse position: {currentGridCell.gridCoordinates} {gridSystem.HasConnections(currentGridCell.gridCoordinates)}");
		}
	}

	private void WorldMouseMarker()
	{
		if (MouseOverUI) return;

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
					mouseMarker.GlobalPosition = cell.gridCoordinates * new Vector3I(1, 1, -1);
				return;
			}
			else
			{
				GD.PrintErr($"GridObject '{gridObject?.Name}' has null GridPositionData or GridCell.");
			}
		}

		// Fall back to position-based lookup
		if (gridSystem != null
		    && gridSystem.TryGetGridCellFromWorldPosition(hitPosition, out GridCell gridCell, true)
		    && gridCell != null)
		{
			if (gridCell.state.HasFlag(Enums.GridCellState.Air))
			{
				gridCell = gridSystem.GetGridCell(gridCell.gridCoordinates - new Vector3I(0, 1, 0));
			}

			if (gridCell != null)
			{
				currentGridCell = gridCell;
				if (mouseMarker != null)
					mouseMarker.GlobalPosition = gridCell.gridCoordinates * new Vector3I(1, 1, -1);
			}
		}
		else
		{
			currentGridCell = GridCell.Null;
			if (mouseMarker != null)
				mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
		}
	}
	
	public GodotObject GetObjectAtMousePosition(out Vector3 worldPosition)
	{
		worldPosition =new Vector3(-1,-1,-1);
		if (IsMouseOverUI())
		{
			//Object will be a UI Object
			return GetViewport()?.GuiGetHoveredControl();
		}
		else
		{
			var space = GetTree().Root.GetWorld3D().DirectSpaceState;
			var mousePosition = GetViewport().GetMousePosition();

			var cam = CameraController.Instance.MainCamera;
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
					mouseMarker.GlobalPosition = currentGridCell.worldCenter;
				return gridObject;
			}
			
			
			
			return node;
		}
	}
	
	#region manager Data
	public override void Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		base.Load(data);
		if(!HasLoadedData) return;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		return null;
	}
	#endregion
	
	public override void Deinitialize()
	{
		return;
	}
	
}
