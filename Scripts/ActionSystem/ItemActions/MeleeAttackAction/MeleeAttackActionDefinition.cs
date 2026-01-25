using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MeleeAttackActionDefinition
	: ItemActionDefinition
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


		if (!gridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions))
		{
			reason = "No grid object Action found";
			return false;
		}

		if (!targetGridCell.HasGridObject())
		{

			reason = "No grid object found";
			return false;
		}

		GridObject targetGridObject = targetGridCell.gridObjects.FirstOrDefault(gridObject =>
		{
			if(gridObject == null) return false;
			if(!gridObject.IsActive) return false;
			if(gridObject == parentGridObject) return false;
			if(gridObject.Team == parentGridObject.Team) return false;
			return true;
		});

		if (targetGridObject == null)
		{
			GD.Print("Target grid object is null, failed all conditions");
		}
		

		
		if (!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell, true, false, out var neighbors))
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
				n.IsWalkable
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
		return parentGridObject.TeamHolder.GetVisibleGridCells().Where(cell =>
		{
			if(!cell.HasGridObject())return false;
			return true;
		}).ToList();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		GD.Print("Valid:" + ValidGridCells.Count.ToString());
		if(!targetGridCell.HasGridObject())
		{
			
			return (targetGridCell, 0);
		}
		GridObject targetGridObject = targetGridCell.gridObjects.FirstOrDefault(gridObject =>
		{
			if(gridObject == null)
			{
				GD.Print("GetValidGridCells: Target grid object is null");
				return false;
			}
			if(!gridObject.IsActive)
			{
				GD.Print("GetValidGridCells: Target grid object is not active ");
				return false;
			}
			if(gridObject == parentGridObject)
			{
				GD.Print("GetValidGridCells: Target grid object is the same as parent");
				return false;
			}
			if(gridObject.Team.HasFlag(parentGridObject.Team))
			{
				GD.Print("GetValidGridCells: Target grid object is on same team");
				return false;
			}
			return true;
		});

		if (targetGridObject == null)
		{
			GD.Print("GetValidGridCells: Target grid object is null, failed all conditions");
			return (targetGridCell, 0);
		}
		else
		{
			return (targetGridCell, 100);
		}
	}
	
	
	public override bool GetIsUIAction() => true;
	public override string GetActionName() => "Melee";
	public override MouseButton GetActionInput() => MouseButton.Left;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => true;
}