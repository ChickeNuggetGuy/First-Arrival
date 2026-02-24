using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class InteractAction : Action, ICompositeAction
{
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }
	
	IInteractableGridobject targetGridObject;
	public InteractAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,
		ActionDefinition parentAction, Godot.Collections.Dictionary<Enums.Stat, int> costs)
		: base(parentGridObject, startingGridCell, targetGridCell ,parentAction, costs)
	{
		targetGridObject = targetGridCell.gridObjects.FirstOrDefault(gridObject =>  gridObject is IInteractableGridobject ) as IInteractableGridobject;
	}

	
	protected override async Task Setup()
	{
		ParentAction = this;

		if (!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell,false, false, out var neighbors))
		{
			GD.PrintErr("InteractAction.Setup: Could not find neighbors for target gridcell");
			return;
		}

		if(!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions)) return;

		// Are we already adjacent?
		bool isAdjacent = neighbors.Any(c => c.GridCoordinates == startingGridCell.GridCoordinates);

		if (isAdjacent)
		{
			// No move needed.
			return;
		}
		
		// Not adjacent. We need to move.
		var walkableNeighbors = neighbors.Where(n => n.IsWalkable).ToList();
		if (!walkableNeighbors.Any())
		{
			GD.PrintErr("InteractAction.Setup: No walkable cell near target to move to.");
			return;
		}

		var moveDestination = walkableNeighbors.OrderBy(n => startingGridCell.GridCoordinates.DistanceSquaredTo(n.GridCoordinates)).First();

		MoveActionDefinition moveActionDefinition =
			gridObjectActions.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition) as MoveActionDefinition;
		
		if (moveActionDefinition == null)
		{
			GD.Print("HELP: Move action definition not found");
			return;
		}
		
		MoveAction moveAction = moveActionDefinition.InstantiateAction(parentGridObject,
			startingGridCell, moveDestination, 
			new Godot.Collections.Dictionary<Enums.Stat, int>()) as MoveAction;
		AddSubAction(moveAction);
	}

	protected override async Task Execute()
	{

		targetGridObject.Interact();
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}
