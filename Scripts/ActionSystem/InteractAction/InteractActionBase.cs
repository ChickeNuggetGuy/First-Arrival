using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class InteractActionBase : ActionBase, ICompositeAction
{
	public ActionBase ParentActionBase { get; set; }
	public List<ActionBase> SubActions { get; set; }
	
	IInteractableGridobject targetGridObject;
	public InteractActionBase(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,
		ActionDefinition parentAction, Godot.Collections.Dictionary<Enums.Stat, int> costs)
		: base(parentGridObject, startingGridCell, targetGridCell ,parentAction, costs)
	{
		targetGridObject = interactActionDefinition.GetTargetGridObject(parentGridObject, targetGridCell) as IInteractableGridobject;
	}

	
	protected override async Task Setup()
		{
			ParentActionBase = this;

			if(!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions)) return;
			if (targetGridObject == null) return;
			var targetCells = new List<GridCell> { targetGridCell };

			GridCell actionAnchor = parentGridObject.GridPositionData.AnchorCell
			                        ?? startingGridCell;
			if (GridFootprintUtility.TryGetAdjacentFacingTarget(
				    parentGridObject,
				    actionAnchor,
				    targetCells,
				    out GridCell facingTarget,
				    out _))
			{
				AddRotateSubActionIfNeeded(actionAnchor, facingTarget);
				return;
			}

			MoveActionDefinition moveActionDefinition =
				gridObjectActions.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition) as MoveActionDefinition;
		
		if (moveActionDefinition == null)
		{
			GD.Print("HELP: Move action definition not found");
			return;
		}
		
			foreach (GridCell moveDestination in GridFootprintUtility
			         .GetAdjacentAnchorCandidates(parentGridObject, targetCells))
			{
				if (!moveActionDefinition.TryBuildCostsOnly(
					    parentGridObject,
					    startingGridCell,
					    moveDestination,
					    out var executionMoveCosts,
					    out _))
					continue;

				if (!GridFootprintUtility.TryGetAdjacentFacingTarget(
					    parentGridObject,
					    moveDestination,
					    targetCells,
					    out GridCell destinationFacingTarget,
					    out _))
					continue;

				MoveActionBase moveActionBase = moveActionDefinition.InstantiateAction(
					parentGridObject,
					startingGridCell,
					moveDestination,
					new Godot.Collections.Dictionary<Enums.Stat, int>()
				) as MoveActionBase;
				AddSubAction(moveActionBase);
				foreach (var moveCost in executionMoveCosts)
				{
					if (costs.ContainsKey(moveCost.Key))
						costs[moveCost.Key] -= moveCost.Value;
				}
				AddRotateSubActionIfNeeded(
					moveDestination,
					destinationFacingTarget,
					force: true
				);
				return;
			}

			GD.PrintErr("InteractAction.Setup: No reachable anchor fits the unit footprint.");
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
