using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.UI;

[GlobalClass]
public partial class MouseHeldInventoryUI : InventoryGridUI
{

	#region Functions

	protected override Task _Setup()
	{
		inventoryGrid = InventoryManager.Instance.GetInventoryGrid(Enums.InventoryType.MouseHeld);
		inventoryGrid.ItemAdded += InventoryGridOnItemAdded;
		inventoryGrid.ItemRemoved += InventoryGridOnItemRemoved;
		base._Setup();
		return Task.CompletedTask;
	}


	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsShown) return;

		// Update position to follow mouse
		Vector2 mousePosition = GetViewport().GetMousePosition();
		Position = mousePosition;
	}

	protected override void _Hide()
	{
		base._Hide();
		Position = new Vector2(-100, -100);
	}

	#region Event Handlers

	private void InventoryGridOnItemRemoved(Item itemREmoved)
	{
		HideCall();
	}

	private void InventoryGridOnItemAdded(Item itemAdded)
	{
		ShowCall();
	}


	#endregion
	#endregion
}