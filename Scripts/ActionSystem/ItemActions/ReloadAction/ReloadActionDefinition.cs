using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class ReloadActionDefinition : ItemActionDefinition
{
	public Item AmmoItem { get; set; }

	public override ActionBase InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	)
	{
		return new ReloadActionBase(parent, startGridCell, targetGridCell, this, costs)
		{
			Item = Item,
			AmmoItem = AmmoItem
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
			reason = "No weapon selected";
			return false;
		}

		AmmoItem ??= FindCompatibleAmmo(gridObject, Item);
		if (!Item.CanReloadWith(AmmoItem, out reason))
			return false;

		AddCost(costs, Enums.Stat.TimeUnits, Item.ItemData.ReloadTimeUnitCost);
		reason = "Success";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		return startingGridCell != null &&
		       Item != null &&
		       Item.CurrentAmmo < Item.AmmoCapacity
			? new List<GridCell> { startingGridCell }
			: new List<GridCell>();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		return (targetGridCell, 0);
	}

	public override bool GetIsUIAction() => false;
	public override string GetActionName() => "Reload";
	public override MouseButton GetActionInput() => MouseButton.None;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => false;

	private static Item FindCompatibleAmmo(GridObject gridObject, Item weapon)
	{
		if (gridObject == null || weapon == null ||
		    !gridObject.TryGetGridObjectNode<GridObjectInventory>(out var inventory))
			return null;

		return inventory.InventoryGrids.Values
			.Where(grid => grid != null)
			.SelectMany(grid => grid.UniqueItems)
			.Where(entry => entry.count > 0 && entry.item != null)
			.Select(entry => entry.item)
			.FirstOrDefault(ammo => weapon.CanReloadWith(ammo, out _));
	}
}
