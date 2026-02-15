using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class RangedAttackActionDefinition
	: ItemActionDefinition
{
	
	[Export] public int range;
	[Export] public int damage;
	public override Action InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs
	)
	{
		return new RangedAttackAction(parent, startGridCell, targetGridCell, this, costs)
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
			reason = "No item equipped";
			return false;
		}

		if (targetGridCell == startingGridCell)
		{
			GD.Print($"starting grid cell {startingGridCell} is equal to {targetGridCell}");
			reason = $"starting grid cell {startingGridCell} is equal to {targetGridCell}";
			return false;
		}

		if (!Item.ItemData.ItemSettings.HasFlag(Enums.ItemSettings.CanRanged))
		{
			reason = "No ranged item equipped";
			return false;
		}

		if (!targetGridCell.HasGridObject())
		{
			reason = "No grid object found";
			return false;
		}

		if (targetGridCell.gridObjects.Contains(parentGridObject))
		{
			reason = "Target grid object is equal to parent";
			return false;
		}

		float distance = startingGridCell.GridCoordinates.DistanceTo(targetGridCell.GridCoordinates);
		if (distance > range)
		{
			reason = "Target is out of range";
			return false;
		}

		// TODO: Add Line of Sight Check

		if (!AddRotateCostsIfNeeded(gridObject, startingGridCell, targetGridCell, costs, out var rotateReason))
		{
			reason = rotateReason;
			return false;
		}

		AddCost(costs, Enums.Stat.TimeUnits, 8 * Item.ItemData.weight);

		reason = "success";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		if (Item == null || !Item.ItemData.ItemSettings.HasFlag(Enums.ItemSettings.CanRanged))
		{
			return new List<GridCell>();
		}

		List<GridCell> tempCells = parentGridObject.TeamHolder.GetVisibleGridCells().Where(cell =>
		{
			if (!cell.HasGridObject()) return false;
			return true;
		}).ToList();
		
		
		// TODO: Add Line of Sight Check
		return tempCells.Where(cell =>
		{
			bool anyValid = false;
			foreach (GridObject gridObject in cell.gridObjects)
			{
				if( gridObject.Team == parentGridObject.Team ||
				    !gridObject.IsActive ) continue;
				anyValid = true;
			}
			
			if(!anyValid) return false;
			
			return cell.gridObjects.Any(gridObject => gridObject.IsActive);
		}).ToList();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		int score = 0;

		if (!targetGridCell.HasGridObject())
		{
			GD.Print("RANGED ATTACK: No grid object found");
			return (null, 0);
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
		
		if(!targetGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) 
			return (targetGridCell, 0);;


		// if (targetGridCell.gridObjects.Contains( parentGridObject))
		// {
		// 	GD.Print("RANGED ATTACK: Target grid object is parent");
		// 	return (null, 0);
		// }
		//
		// if (!targetGridCell.gridObjects.Any(gridObject => gridObject.IsActive))
		// {
		// 	GD.Print("RANGED ATTACK: Grid object is not active");
		// 	return (null, 0);
		// }
		//
		// if (targetGridCell.gridObjects.Any(gridObject => gridObject.Team.HasFlag(parentGridObject.Team)))
		// {
		// 	GD.Print($"RANGED ATTACK: Target grid object team is equal to parent: {parentGridObject.Team}");
		// }


		score += 80; // Base score for attacking an enemy
		// Add more score based on enemy health (e.g., higher score for lower health)
		if (statHolder.TryGetStat(Enums.Stat.Health, out var healthStat))
		{
			score += (100 - Mathf.RoundToInt(healthStat.CurrentValue)); // Higher score for lower health
		}

		return (targetGridCell, score);
	}

	public override bool GetIsUIAction() => true;
	public override string GetActionName() => "Ranged";
	public override MouseButton GetActionInput() => MouseButton.Left;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => true;
}