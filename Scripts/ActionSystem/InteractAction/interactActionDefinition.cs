using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class interactActionDefinition : ActionDefinition
{
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell, Dictionary<Enums.Stat, int> costs)
	{
		return new InteractAction(parent, startGridCell, targetGridCell,this, costs);
	}

	protected override bool OnValidateAndBuildCosts(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs, out string reason)
	{

		if (!targetGridCell.HasGridObject())
		{
			{
				reason = $"No grid object found {targetGridCell.gridObjects.Count}";
				return false;
			}
		}

		if (!gridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions))
		{
			reason = "Grid object Actions not found";
			return false;
		}
		
		GridObject targetGridObject = targetGridCell.gridObjects.FirstOrDefault(gridObject =>
		{
			if(gridObject == null) return false;
			if(!gridObject.IsActive) return false;
			if(gridObject == parentGridObject) return false;
			if(gridObject is not IInteractableGridobject) return false;
			return true;
		});

		if (targetGridObject == null)
		{
			GD.Print("Target grid object is null, failed all conditions");
			reason = "No target grid object found";
			return false;
		}
		

		IInteractableGridobject interactable = targetGridObject as IInteractableGridobject;
		if (interactable == null)
		{
			reason = "Target grid object is not interactable";
			return false;
		}
		if (!GridSystem.Instance.TryGetGridCellsNeighbors(interactable.GetInteractableCells(),false,false, out var neighbors))
		{
			reason = "Could not find neighbors for target gridcell";
			return false;
		}

		

		// Already adjacent?
		if (neighbors.Any(gridCell => gridCell.gridCoordinates == startingGridCell.gridCoordinates))
		{
			// Face the target if needed
			if (
				!AddRotateCostsIfNeeded(
					gridObject,
					startingGridCell,
					targetGridCell,
					costs,
					out var rotateReason
				)
			)
			{
				reason = rotateReason;
				return false;
			}
		}
		else
		{
			// Need to move to an adjacent tile first
			var walkableNeighbors = neighbors.Where(n =>
				n.IsWalkable && Pathfinder.Instance.IsPathPossible(startingGridCell.gridCoordinates, n.gridCoordinates)
			).ToList();

			if (walkableNeighbors.Count == 0)
			{
				reason = "No adjacent walkable cell near target";
				return false;
			}

			var targetAdjacent = walkableNeighbors.OrderBy(n =>
				startingGridCell.gridCoordinates.DistanceSquaredTo(n.gridCoordinates)
			).First();

			var moveAction =
				gridObjectActions.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition)
					as MoveActionDefinition;

			if (moveAction == null)
			{
				reason = "unit cannot move when it is needed";
				return false;
			}

			if (
				!moveAction.TryBuildCostsOnly(
					gridObject,
					startingGridCell,
					targetAdjacent,
					out var moveCosts,
					out var moveReason
				)
			)
			{
				reason = $"move Action validation failed: {moveReason}";
				return false;
			}

			AddCosts(costs, moveCosts);

			if (
				!AddRotateCostsIfNeeded(
					gridObject,
					targetAdjacent,
					targetGridCell,
					costs,
					out var rotateReason2
				)
			)
			{
				reason = rotateReason2;
				return false;
			}
		}

		foreach (KeyValuePair<Enums.Stat, int> cost in interactable.costs)
		{
			AddCost(costs ,cost.Key, cost.Value);
		}

		reason = "success";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		List<GridCell> validCells = GridSystem.Instance.AllGridCells.Where(cell =>
		{
			if (!cell.HasGridObject()) return false;

			if (!cell.gridObjects.Any(gridObject => gridObject is IInteractableGridobject interactableGridObject)) return false;
			return true;
		}).ToList();
		return validCells;
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		GridObject targetGridObject = targetGridCell.gridObjects.FirstOrDefault(gridObject =>
		{
			if(gridObject == null) return false;
			if(gridObject is IInteractableGridobject) return false;
			if(!gridObject.IsActive) return false;
			if(gridObject == parentGridObject) return false;
		
			return true;
		});

		if (targetGridObject == null)
		{
			GD.Print("Target grid object is null, failed all conditions");
			return (targetGridCell, 0);
		}
			
		else
		{
			return (targetGridCell, 85);
		}
	}

	public override bool GetIsUIAction() => true;

	public override string GetActionName() => "Interact";

	public override MouseButton GetActionInput() => MouseButton.Left;

	public override bool GetIsAlwaysActive() => false;

	public override bool GetRemainSelected() => false;
}
