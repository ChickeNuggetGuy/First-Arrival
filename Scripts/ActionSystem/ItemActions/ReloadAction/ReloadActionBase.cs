using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class ReloadActionBase : ActionBase, IItemAction
{
	public Item Item { get; set; }
	public Item AmmoItem { get; set; }
	private bool _reloadSucceeded;

	public ReloadActionBase(
		GridObject parentGridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		ActionDefinition parentAction,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	) : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
	{
	}

	protected override Task Setup() => Task.CompletedTask;

	protected override Task Execute()
	{
		string reason = "No weapon selected";
		_reloadSucceeded = Item != null && Item.TryReloadWith(AmmoItem, out reason);
		if (!_reloadSucceeded)
			GD.PrintErr($"Reload failed: {reason}");
		return Task.CompletedTask;
	}

	protected override Task ActionComplete() => Task.CompletedTask;

	protected override bool ShouldDeductCosts() => _reloadSucceeded;
}
