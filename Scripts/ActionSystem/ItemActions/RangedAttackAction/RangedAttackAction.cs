using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class RangedAttackAction : Action, IItemAction
{
	public Item Item { get; set; }

	public RangedAttackAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, ActionDefinition parentAction,
		Dictionary<Enums.Stat, int> costs) : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
	{
	}

	protected override async Task Setup()
	{
		// No setup needed for a simple ranged attack
		return;
	}

	protected override async Task Execute()
	{
		GridObject targetGridObject = targetGridCell.currentGridObject;

		if (targetGridObject == null)
		{
			GD.PrintErr("RangedAttackAction.Execute: Target grid object is null.");
			return;
		}

		if (!targetGridObject.TryGetStat(Enums.Stat.Health, out var health))
		{
			GD.Print("Target Grid Object does not have Health stat");
			return;
		}

		if (Item.ItemData.ItemSettings.HasFlag(Enums.ItemSettings.CanRanged))
		{
			health.RemoveValue(Item.ItemData.Damage);
			GD.Print($"Target unit: {targetGridObject} Damaged for {Item.ItemData.Damage} damage, remaining health is {health.CurrentValue}");
		}
	}

	protected override async Task ActionComplete()
	{
		return;
	}
}