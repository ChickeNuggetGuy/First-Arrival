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

	[Export] TextureRect itemIcon;
	[Export] Label itemCountLabel;
	public Item Item { get; private set; }

	#endregion


	public void Init(InventoryGridUI parentGridUI, Vector2I inventoryCoords)
	{
		this.inventoryCoords = inventoryCoords;
		this.parentGridUI = parentGridUI;
		// Every atlas region must fill its cell; preserving each slice's aspect ratio
		// independently adds padding and makes one multi-cell icon look chopped apart.
		itemIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
		Pressed += ButtonOnPressed;
		MouseFilter = MouseFilterEnum.Stop;
	}

	private void ButtonOnPressed()
	{
		parentGridUI.ItemSlot_Pressed(this);
	}


	public void SetItem(Item item, int count, Vector2I localCoords, bool isRoot)
	{
		Item = item;
		if (item == null || item.ItemData == null)
		{
			itemIcon.Texture = null;
			itemCountLabel.Text = "";
			return;
		}

		if (isRoot && item.IsRangedWeapon)
			itemCountLabel.Text = $"{item.CurrentAmmo}/{item.AmmoCapacity}";
		else
			itemCountLabel.Text = (isRoot && count > 1) ? count.ToString() : "";

		TooltipText = item.IsRangedWeapon
			? $"{item.ItemData.ItemName} ({item.CurrentAmmo}/{item.AmmoCapacity} loaded)"
			: item.ItemData.ItemName;

		// Texture Slicing Logic
		if (parentGridUI.InventoryGrid.InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
		{
			AtlasTexture atlasTex = new AtlasTexture
			{
				Atlas = item.ItemData.ItemIcon,
				Region = item.ItemData.GetTextureRegionForCell(localCoords.X, localCoords.Y),
				FilterClip = true
			};

			itemIcon.Texture = atlasTex;
		}
		else
		{
			//Use full texture
			itemIcon.Texture = item.ItemData.ItemIcon;
		}
	}

	public Dictionary<string, Callable> GetContextActions()
	{
		if (parentGridUI == null || parentGridUI.InventoryGrid == null)
		{
			GD.Print("ItemSlotUI.GetContextActions(): parentGridUI or InventoryGrid is null");
			return null;
		}

		if (!parentGridUI.InventoryGrid.TryGetItemAt(inventoryCoords.X, inventoryCoords.Y, out var itemInfo))
		{
			GD.Print("GetContextActions(): item == null");
			return null;
		}

		return itemInfo.item?.GetContextActions();
	}
}
