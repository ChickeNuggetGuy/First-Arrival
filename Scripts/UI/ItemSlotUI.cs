using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using Godot;

namespace FirstArrival.Scripts.UI;

[GlobalClass]
public partial class ItemSlotUI : Button, IContextUser<ItemSlotUI>
{
	
	#region Variables
	public ItemSlotUI parent
	{
		get => this; set{} }
	protected InventoryGridUI parentGridUI;
	public Vector2I inventoryCoords;
	#endregion


	public void Init(InventoryGridUI parentGridUI, Vector2I inventoryCoords)
	{
		this.inventoryCoords  = inventoryCoords;
		this.parentGridUI = parentGridUI;
		Pressed += ButtonOnPressed;
	}

	private void ButtonOnPressed()
	{
		GD.Print("ButtonOnPressed");
		parentGridUI.ItemSlot_Pressed(this);
	}

	
	public void SetItem(Item item, Vector2I coords)
	{
		if (item == null)
		{ 
			Icon = null;
			return;
		}

		Icon = item.ItemData.ItemIcon;
	}

	public Dictionary<string,Callable> GetContextActions()
	{
		if(parentGridUI.InventoryGrid == null)
		{
			GD.Print("ItemSlotUI.GetContextActions(): parentGridUI.inventoryGrid == null");
			return null;
		}
		if (!parentGridUI.InventoryGrid.TryGetItemAt(inventoryCoords.X, inventoryCoords.Y, out Item item))
		{
			GD.Print("GetContextActions(): item == null");
			return null;
		}
		return item.GetContextActions();
	}


}