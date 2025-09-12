using Godot;
using System;
using System.Threading.Tasks;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class InputManager : Manager<InputManager>
{
	public GridCell currentGridCell
	{
		get;
		protected set;
	}
	
	[Export] private Node3D mouseMarker;
	public bool MouseOverUI
	{
		get => IsMouseOverUI(); private set{}}
	protected override Task _Setup()
	{
		return Task.CompletedTask;
	}

	protected override Task _Execute()
	{
		return Task.CompletedTask;
	}
	private bool IsMouseOverUI() => GetViewport()?.GuiGetHoveredControl() != null;
	public override void _Process(double delta)
	{
		base._Process(delta);
		if( !ExecuteComplete) return;
		WorldMouseMarker();
		//GD.Print(currentGridCell.gridCoordinates);
	}

	public override void _Input(InputEvent @event)
	{
		if( !ExecuteComplete) return;
		base._Input(@event);
		
	}

	private void WorldMouseMarker()
	{
		if (MouseOverUI) return;
		Node node =	GetObjectAtMousePosition(out Vector3 hitPosition) as Node;
		
		if (node == null) return;
		
		if (node is GridObject gridObject)
		{
			var cell = gridObject.GridPositionData?.GridCell;
			if (cell != null)
			{
				currentGridCell = cell;
				if (mouseMarker != null)
					mouseMarker.GlobalPosition = cell.worldCenter;
				return;
			}

			GD.PrintErr(
				$"GridObject '{gridObject?.Name}' has null GridPositionData or GridCell."
			);
			// Fall through to position-based lookup.
		}

		if (GridSystem.Instance != null
		    && GridSystem.Instance.TyGetGridCellFromWorldPosition(
			    hitPosition,
			    out GridCell gridCell,
			    true
		    )
		    && gridCell != null)
		{
			currentGridCell = gridCell;
			if (mouseMarker != null)
				mouseMarker.GlobalPosition = gridCell.worldCenter;
		}
		else
		{
			currentGridCell = GridCell.Null;
			if (mouseMarker != null)
				mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
		}
		// GD.Print($"currentGridCell: {currentGridCell.gridCoordinates}");
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
			var node = colliderObject as Node;
			return node;
		}
	}
	
}
