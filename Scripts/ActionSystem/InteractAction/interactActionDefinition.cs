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
	protected override bool TargetsGridObjects => true;

	public override ActionBase InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs)
	{
		return new InteractActionBase(parent, startGridCell, targetGridCell,this, costs);
	}

	protected override bool OnValidateAndBuildCosts(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs, out string reason)
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
		
		GridObject targetGridObject = GetTargetGridObject(gridObject, targetGridCell);

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
			// Reach the selected interaction cell, not any other part of the object.
			var targetCells = new List<GridCell> { targetGridCell };

			GridCell actionAnchor = gridObject.GridPositionData.AnchorCell
			                        ?? startingGridCell;
			if (GridFootprintUtility.TryGetAdjacentFacingTarget(
				    gridObject,
				    actionAnchor,
				    targetCells,
				    out GridCell facingTarget,
				    out _))
			{
				if (!AddRotateCostsIfNeeded(
					    gridObject,
					    actionAnchor,
					    facingTarget,
					    costs,
					    out string rotateReason,
					    actionAnchor))
				{
					reason = rotateReason;
					return false;
				}
			}
			else
			{
				var moveAction = gridObjectActions.ActionDefinitions
					.FirstOrDefault(action => action is MoveActionDefinition)
					as MoveActionDefinition;
				if (moveAction == null)
				{
					reason = "Unit cannot move when movement is required";
					return false;
				}

				bool foundDestination = false;
				string lastMoveReason = "No adjacent anchor fits the unit footprint";
				foreach (GridCell candidate in GridFootprintUtility
				         .GetAdjacentAnchorCandidates(gridObject, targetCells))
				{
					if (!moveAction.TryBuildCostsOnly(
						    gridObject,
						    startingGridCell,
						    candidate,
						    out var moveCosts,
						    out lastMoveReason))
						continue;

					if (!GridFootprintUtility.TryGetAdjacentFacingTarget(
						    gridObject,
						    candidate,
						    targetCells,
						    out GridCell candidateFacingTarget,
						    out _))
						continue;

					var combinedCosts = new Godot.Collections.Dictionary<Enums.Stat, int>();
					AddCosts(combinedCosts, moveCosts);
					Enums.Direction arrivalDirection = moveAction.path?.Count >= 2
						? RotationHelperFunctions.GetDirectionBetweenCells(
							moveAction.path[^2],
							moveAction.path[^1]
						)
						: gridObject.GridPositionData.Direction;
					if (!AddRotateCostsIfNeeded(
						    gridObject,
						    candidate,
						    candidateFacingTarget,
						    combinedCosts,
						    out _,
						    candidate,
						    arrivalDirection))
						continue;

					AddCosts(costs, combinedCosts);
					foundDestination = true;
					break;
				}

				if (!foundDestination)
				{
					reason = $"Cannot reach interaction range: {lastMoveReason}";
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

	internal static GridObject GetTargetGridObject(GridObject actor, GridCell cell)
	{
		return cell?.gridObjects?.FirstOrDefault(target =>
			target != null && target.IsActive && target != actor &&
			target is IInteractableGridobject interactable &&
			(interactable.GetInteractableCells()?.Contains(cell) ?? false));
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		return GridSystem.Instance?.AllGridCells?
			.Where(cell => GetTargetGridObject(gridObject, cell) != null)
			.ToList() ?? new List<GridCell>();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		return (targetGridCell, GetTargetGridObject(parentGridObject, targetGridCell) != null ? 85 : 0);
	}

	public override bool GetIsUIAction() => true;

	public override string GetActionName() => "Interact";

	public override MouseButton GetActionInput() => MouseButton.Left;

	public override bool GetIsAlwaysActive() => false;

	public override bool GetRemainSelected() => false;
}
