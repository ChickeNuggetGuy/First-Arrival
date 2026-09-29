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
	protected override bool TargetsGridObjects => true;

	[Export] public Enums.RangedAttackType Type;
	[Export] public int attackCount = 1;
	[Export(PropertyHint.Range, "1,100,1")] public int ammoCost = 1;
	[Export] public int range;
	[Export] public bool canCauseFatalWounds = true;
	[Export] public bool dealsStunDamage = false;

	// Legacy per-action ammo links remain supported while weapon resources migrate
	// to ItemData.AmmoItem. Damage still comes from the linked ammo ItemData.
	[Export] public ItemData ammoItem;

	[Export(PropertyHint.Range, "-100,100,1")] public float accuracy = 0f;
	[Export] public Godot.Collections.Dictionary<Enums.Stat, int> damagingStats = new();

	[ExportGroup("Action Cost")]
	[Export] public Godot.Collections.Dictionary<Enums.Stat, int> actionCosts = new()
	{
		{ Enums.Stat.TimeUnits, 20 }
	};
	
	public override ActionBase InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	)
	{
		return new RangedAttackActionBase(parent, startGridCell, targetGridCell, this, costs)
		{
			Item = Item
		};
	}

	protected override bool OnValidateAndBuildCosts(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs,
		out string reason
	)
	{
		if (Item == null)
		{
			reason = "No item equipped";
			return false;
		}

		ItemData ammo = Item.GetRequiredAmmoItem();
		if (Item.AmmoCapacity <= 0 || ammo == null || !ammo.IsAmmunition)
		{
			reason = "Weapon ammo is not configured";
			return false;
		}

		if (ammo.AmmoDamage <= 0)
		{
			reason = $"{ammo.ItemName} has no ranged damage configured";
			return false;
		}

		if (!Item.HasAmmoForAttack(ammoCost))
		{
			reason = $"Not enough ammo ({Item.CurrentAmmo}/{Item.AmmoCapacity}; requires {ammoCost})";
			return false;
		}

		if (targetGridCell == startingGridCell)
		{
			GD.Print($"starting grid cell {startingGridCell} is equal to {targetGridCell}");
			reason = $"starting grid cell {startingGridCell} is equal to {targetGridCell}";
			return false;
		}
		
		if (!targetGridCell.HasGridObject())
		{
			reason = "No grid object found";
			return false;
		}

		GridObject targetGridObject = targetGridCell.gridObjects.FirstOrDefault(candidate =>
			candidate != null &&
			candidate.IsActive &&
			candidate != gridObject &&
			candidate.Team != gridObject.Team);
		if (targetGridObject == null)
		{
			reason = "No active hostile target found";
			return false;
		}

		List<GridCell> sourceCells = GridFootprintUtility.GetOccupiedCells(
			gridObject,
			startingGridCell
		);
		var targetCells = new List<GridCell> { targetGridCell };
		float distance = GridFootprintUtility.GetClosestGridDistance(
			sourceCells,
			targetCells
		);
		if (distance > range)
		{
			reason = "Target is out of range";
			return false;
		}

		// TODO: Add Line of Sight Check

		GridCell actionAnchor = gridObject.GridPositionData.AnchorCell
		                        ?? startingGridCell;
		GridCell facingTarget = targetCells
			.OrderBy(cell => actionAnchor.GridCoordinates.DistanceSquaredTo(
				cell.GridCoordinates))
			.First();
		if (!AddRotateCostsIfNeeded(
			    gridObject,
			    actionAnchor,
			    facingTarget,
			    costs,
			    out var rotateReason,
			    actionAnchor))
		{
			reason = rotateReason;
			return false;
		}

		AddCosts(costs, actionCosts);


		reason = "success";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		if (Item == null || !Item.HasAmmoForAttack(ammoCost))
			return new List<GridCell>();

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





		score += 80;
		if (statHolder.TryGetStat(Enums.Stat.Health, out var healthStat))
		{
			score += (100 - Mathf.RoundToInt(healthStat.CurrentValue));
		}

		return (targetGridCell, score);
	}

	public override bool GetIsUIAction() => true;
	public override string GetActionName() => "Ranged";
	public override MouseButton GetActionInput() => MouseButton.Left;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => true;
}
