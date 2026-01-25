using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;

public partial class BuySellUI : UIWindow
{
	[Export] protected Tree itemTreeUI;
	[Export] protected Texture2D buyTexture;
	[Export] protected Texture2D sellTexture;
	protected override Task _Setup()
	{
		itemTreeUI.HideRoot = true;
		itemTreeUI.SetColumnTitle(0, "Item");
		itemTreeUI.SetColumnTitleAlignment(0, HorizontalAlignment.Center);
		
		itemTreeUI.SetColumnTitle(1, "Buy");
		itemTreeUI.SetColumnTitleAlignment(1, HorizontalAlignment.Right);
		
		itemTreeUI.SetColumnTitle(2, "QTY");
		itemTreeUI.SetColumnTitleAlignment(2, HorizontalAlignment.Center);
		
		itemTreeUI.SetColumnTitle(3, "Sell");
		itemTreeUI.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
		
		return base._Setup();
	}

	protected override void _Show()
	{
		DrawTree();
		base._Show();
	}

	private void DrawTree()
	{
		itemTreeUI.Clear();
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager == null) return;


		TreeItem root = itemTreeUI.CreateItem();
		foreach (ItemData itemData in inventoryManager.Database.GetAllItems())
		{
			TreeItem subItem =  itemTreeUI.CreateItem(root);
			
			//Set Tree item data (name, counts, etc)
			subItem.SetText(0,itemData.ItemName);
			subItem.AddButton(1,buyTexture );
			subItem.SetText(2, "QTY");
			subItem.AddButton(3,sellTexture );
		}
	}
}
