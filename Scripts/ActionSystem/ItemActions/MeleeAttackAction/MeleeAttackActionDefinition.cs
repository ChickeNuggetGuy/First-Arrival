using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MeleeAttackActionDefinition
	: ActionDefinition,
		IItemActionDefinition
{
	public Item Item { get; set; }

	public override Action InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs
	)
	{
		return new MeleeAttackAction(parent, startGridCell, targetGridCell, this, costs)
		{
			Item = Item
		};
	}

	protected override bool OnValidateAndBuildCosts(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs,
		out string reason
	)
	{
		if (Item == null)
		{
			reason = "No melee item equipped";
			return false;
		}
		
		if(!Item.ItemData.ItemSettings.HasFlag(Enums.ItemSettings.CanMelee))
		{
			reason = "No melee item equipped";
			return false;
		}

		if (!targetGridCell.HasGridObject())
		{
			{
				reason = "No grid object found";
				return false;
			}
		}
		
		if (targetGridCell.currentGridObject == parentGridObject)
		{
			{
				reason = "Target grid object is equal to parent";
				return false;
			}
		}

		
		if (!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell, out var neighbors))
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
				n.state.HasFlag(Enums.GridCellState.Walkable)
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
				gridObject.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition)
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

		AddCost(costs, Enums.Stat.TimeUnits, 6 * Item.ItemData.weight);
		AddCost(costs, Enums.Stat.Stamina, 8 * Item.ItemData.weight);

		reason = "success";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		if (
			!GridSystem.Instance.TryGetGridCellsInRange(
				startingGridCell,
				new Vector2I(20, 5),
				out List<GridCell> neighbors
			)
		)
		{
			return new List<GridCell>();
		}

		return neighbors.Where(n => n.HasGridObject()).ToList();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		if (!targetGridCell.HasGridObject() || targetGridCell.currentGridObject.Team.HasFlag(parentGridObject.Team) || !targetGridCell.currentGridObject.IsActive) 
		{
			return (targetGridCell, 0);
		}
		else
		{
			return (targetGridCell, 85);
		}
	}
	
	
	public override bool GetIsUIAction() => true;
	public override string GetActionName() => "Melee";
	public override MouseButton GetActionInput() => MouseButton.Left;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => true;
}