using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class RangedAttackActionDefinition
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
			reason = "No ranged item equipped";
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

		if (targetGridCell.currentGridObject == parentGridObject)
		{
			reason = "Target grid object is equal to parent";
			return false;
		}

		float distance = startingGridCell.gridCoordinates.DistanceTo(targetGridCell.gridCoordinates);
		if (distance > Item.ItemData.Range)
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
		AddCost(costs, Enums.Stat.Stamina, 6 * Item.ItemData.weight);

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

		if (
			!GridSystem.Instance.TryGetGridCellsInRange(
				startingGridCell,
				new Vector2I(Item.ItemData.Range, Item.ItemData.Range),
				out List<GridCell> cellsInRange
			)
		)
		{
			return new List<GridCell>();
		}

		// TODO: Add Line of Sight Check
		return cellsInRange.Where(cell => cell.HasGridObject() && cell.currentGridObject.Team != parentGridObject.Team).ToList();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		// Placeholder implementation:
		// You would implement actual AI logic here to determine the score
		// based on factors like target health, distance, tactical advantage, etc.
		int score = 0;

		if (!targetGridCell.HasGridObject())
		{
			GD.Print("RANGED ATTACK: No grid object found");
			return (null, 0);
		}

		if (targetGridCell.currentGridObject == parentGridObject)
		{
			GD.Print("RANGED ATTACK: Target grid object is equal to parent");
			return (null, 0);
		}

		if (!targetGridCell.currentGridObject.IsActive)
		{
			GD.Print("RANGED ATTACK: Grid object is not active");
			return (null, 0);
		}

		if (targetGridCell.currentGridObject.Team.HasFlag(parentGridObject.Team))
		{
			GD.Print($"RANGED ATTACK: Target grid object team: {targetGridCell.currentGridObject.Team} is equal to parent: {parentGridObject.Team}");
		}


		score += 80; // Base score for attacking an enemy
		// Add more score based on enemy health (e.g., higher score for lower health)
		if (targetGridCell.currentGridObject.TryGetStat(Enums.Stat.Health, out var healthStat))
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