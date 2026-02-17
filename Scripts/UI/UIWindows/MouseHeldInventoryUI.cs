using System.Collections.Generic;
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
		InventoryGrid = InventoryManager.Instance.GetInventoryGrid(Enums.InventoryType.MouseHeld);
		InventoryGrid.ItemAdded += InventoryGridOnItemAdded;
		InventoryGrid.ItemRemoved += InventoryGridOnItemRemoved;
		SetupInventoryUI(InventoryGrid);
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

	private void InventoryGridOnItemRemoved(InventoryGrid inventoryGrid, Item itemREmoved)
	{
		HideCall();
	}

	private void InventoryGridOnItemAdded(InventoryGrid inventoryGrid, Item itemAdded)
	{
		ShowCall();
	}


	#endregion
	#endregion
}