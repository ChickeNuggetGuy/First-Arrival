using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.UI;

[GlobalClass]
public partial class ItemSlotUI : Button, IContextUser<ItemSlotUI>
{
	#region Variables

	public ItemSlotUI parent
	{
		get => this;
		set { }
	}

	protected InventoryGridUI parentGridUI;
	public Vector2I inventoryCoords;

	[Export] Label itemCountLabel;
	public Item Item { get; private set; }

	#endregion


	public void Init(InventoryGridUI parentGridUI, Vector2I inventoryCoords)
	{
		this.inventoryCoords = inventoryCoords;
		this.parentGridUI = parentGridUI;
		Pressed += ButtonOnPressed;
		MouseFilter = MouseFilterEnum.Stop;
	}

	private void ButtonOnPressed()
	{
		// Debug slot state
		if (Item != null)
			GD.Print($"  - Slot UI thinks it has: {Item.ItemData?.ItemName}");
		else
			GD.Print($"  - Slot UI thinks it is Empty");

		// Debug grid state verification
		if (parentGridUI != null && parentGridUI.InventoryGrid != null)
		{
			if (parentGridUI.InventoryGrid.TryGetItemAt(inventoryCoords.X, inventoryCoords.Y, out var itemInfo))
			{
				if (itemInfo.item != null)
					GD.Print($"  - Grid verification: Found {itemInfo.item.ItemData?.ItemName} x{itemInfo.count}");
				else
					GD.Print($"  - Grid verification: Empty/Null item returned");
			}
			else
			{
				GD.Print($"  - Grid verification: Slot empty");
			}
		}

		parentGridUI.ItemSlot_Pressed(this);
	}


	public void SetItem(Item item, int count, Vector2I localCoords, bool isRoot)
	{
		Item = item;
		if (item == null || item.ItemData == null)
		{
			Icon = null;
			itemCountLabel.Text = "";
			return;
		}

		// Only show the count if this is the root cell AND count is > 1
		itemCountLabel.Text = (isRoot && count > 1) ? count.ToString() : "";

		// Texture Slicing Logic
		AtlasTexture atlasTex = new AtlasTexture();
		atlasTex.Atlas = item.ItemData.ItemIcon;
		atlasTex.Region = item.ItemData.GetTextureRegionForCell(localCoords.X, localCoords.Y);

		Icon = atlasTex;
	}

	public Dictionary<string, Callable> GetContextActions()
	{
		if (parentGridUI.InventoryGrid == null)
		{
			GD.Print("ItemSlotUI.GetContextActions(): parentGridUI.inventoryGrid == null");
			return null;
		}

		if (!parentGridUI.InventoryGrid.TryGetItemAt(inventoryCoords.X, inventoryCoords.Y, out var itemInfo))
		{
			GD.Print("GetContextActions(): item == null");
			return null;
		}

		return itemInfo.item.GetContextActions();
	}
}