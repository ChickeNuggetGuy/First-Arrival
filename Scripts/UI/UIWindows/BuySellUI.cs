using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class BuySellUI : UIWindow
{
	[Export] protected Tree itemTreeUI;
	[Export] protected Texture2D buyTexture;
	[Export] protected Texture2D sellTexture;
	[Export] protected Button confirmButton;
	[Export] protected Button cancelButton;
	[Export] protected Label finalValueLabel;

	private readonly Dictionary<int, int> currentItemChange = new();
	private readonly Dictionary<int, TreeItem> treeItems = new();

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

		itemTreeUI.ButtonClicked += ItemTreeUIOnButtonClicked;
		if (confirmButton != null) confirmButton.Pressed += ConfirmButtonOnPressed;
		if (cancelButton != null) cancelButton.Pressed += CancelButtonOnPressed;
		return base._Setup();
	}

	private async void ConfirmButtonOnPressed()
	{
		try
		{
			if (!FinalizeTransaction()) return;
			await HideCall();
		}
		catch (Exception exception)
		{
			GD.PrintErr($"Failed to finalize transaction: {exception.Message}\n{exception.StackTrace}");
		}
	}

	private async void CancelButtonOnPressed()
	{
		try
		{
			currentItemChange.Clear();
			await HideCall();
		}
		catch (Exception exception)
		{
			GD.PrintErr($"Failed to close buy/sell window: {exception.Message}");
		}
	}

	private void ItemTreeUIOnButtonClicked(TreeItem item, long column, long id, long mouseButtonIndex)
	{
		if (mouseButtonIndex != (long)MouseButton.Left) return;

		if (column == 1) ChangePendingQuantity((int)id, 1);
		else if (column == 3) ChangePendingQuantity((int)id, -1);
	}

	private void ChangePendingQuantity(int itemId, int amount)
	{
		TeamBaseCellDefinition teamBase = GameManager.Instance.currentBase;
		ItemData itemData = InventoryManager.Instance?.GetItemData(itemId);
		if (teamBase == null || itemData == null) return;

		int oldChange = currentItemChange.GetValueOrDefault(itemId, 0);
		int newChange = oldChange + amount;
		if (GetOwnedCount(teamBase, itemData) + newChange < 0) return;
		if (itemData is Craft && newChange < 0 &&
		    teamBase.GetSellableCraftCountForItem(itemId) + newChange < 0) return;

		if (itemData is Craft)
		{
			int pendingCraftChange = GetPendingCraftChange() - oldChange + newChange;
			int resultingCraftCount = teamBase.CraftCount + pendingCraftChange;
			if (resultingCraftCount < 0 || resultingCraftCount > teamBase.MaxCraft) return;
		}

		if (newChange == 0) currentItemChange.Remove(itemId);
		else currentItemChange[itemId] = newChange;

		UpdateDisplayedQuantity(itemId);
		RefreshTransactionSummary();
	}

	private int GetPendingCraftChange()
	{
		int change = 0;
		foreach (KeyValuePair<int, int> pair in currentItemChange)
		{
			if (InventoryManager.Instance.GetItemData(pair.Key) is Craft)
				change += pair.Value;
		}
		return change;
	}

	private int GetTransactionCost()
	{
		int cost = 0;
		foreach (KeyValuePair<int, int> pair in currentItemChange)
		{
			ItemData itemData = InventoryManager.Instance.GetItemData(pair.Key);
			if (itemData == null) continue;

			cost += pair.Value > 0
				? itemData.buyPrice * pair.Value
				: itemData.sellPrice * pair.Value;
		}
		return cost;
	}

	private void RefreshTransactionSummary()
	{
		int cost = GetTransactionCost();
		int resultingFunds = GameManager.Instance.currentBaseFunds - cost;
		if (finalValueLabel != null)
			finalValueLabel.Text = $"Transaction: {(cost >= 0 ? "Cost" : "Credit")} ${Math.Abs(cost):N0} | Funds: ${resultingFunds:N0}";

		if (confirmButton != null)
			confirmButton.Disabled = currentItemChange.Count == 0 || resultingFunds < 0;
	}

	private bool FinalizeTransaction()
	{
		TeamBaseCellDefinition teamBase = GameManager.Instance.currentBase;
		if (teamBase == null || currentItemChange.Count == 0) return false;

		int cost = GetTransactionCost();
		if (GameManager.Instance.currentBaseFunds < cost) return false;
		if (!ValidateFinalQuantities(teamBase)) return false;

		foreach (KeyValuePair<int, int> pair in currentItemChange)
		{
			if (pair.Value >= 0) continue;
			ItemData itemData = InventoryManager.Instance.GetItemData(pair.Key);
			int removeCount = -pair.Value;

			if (itemData is Craft)
			{
				for (int i = 0; i < removeCount; i++)
					teamBase.TryRemoveCraftByItemId(pair.Key);
			}
			else
			{
				teamBase.TryRemoveItem(pair.Key, removeCount);
			}
		}

		foreach (KeyValuePair<int, int> pair in currentItemChange)
		{
			if (pair.Value <= 0) continue;
			ItemData itemData = InventoryManager.Instance.GetItemData(pair.Key);

			if (itemData is Craft craftData)
			{
				for (int i = 0; i < pair.Value; i++)
					teamBase.TryAddCraftWithoutPurchase(
						Enums.CraftStatus.Home,
						(Craft)craftData.Duplicate(true));
			}
			else
			{
				teamBase.TryAddItem(pair.Key, pair.Value);
			}
		}

		GameManager.Instance.currentBaseFunds -= cost;
		if (!GameManager.Instance.SyncCurrentBaseToGlobeState())
			GD.PrintErr("Transaction completed locally, but the globe transition state could not be updated.");
		currentItemChange.Clear();
		return true;
	}

	private bool ValidateFinalQuantities(TeamBaseCellDefinition teamBase)
	{
		int finalCraftCount = teamBase.CraftCount + GetPendingCraftChange();
		if (finalCraftCount < 0 || finalCraftCount > teamBase.MaxCraft) return false;

		foreach (KeyValuePair<int, int> pair in currentItemChange)
		{
			ItemData itemData = InventoryManager.Instance.GetItemData(pair.Key);
			if (itemData == null || GetOwnedCount(teamBase, itemData) + pair.Value < 0)
				return false;
			if (itemData is Craft && pair.Value < 0 &&
			    teamBase.GetSellableCraftCountForItem(pair.Key) < -pair.Value)
				return false;
		}
		return true;
	}

	protected override void _Show()
	{
		DrawTree();
		base._Show();
	}

	private void DrawTree()
	{
		currentItemChange.Clear();
		treeItems.Clear();
		itemTreeUI.Clear();

		TeamBaseCellDefinition teamBase = GameManager.Instance.currentBase;
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (teamBase == null || inventoryManager?.Database == null)
		{
			GD.PrintErr("Cannot draw buy/sell window without a base and item database.");
			return;
		}

		TreeItem root = itemTreeUI.CreateItem();
		foreach (ItemData itemData in inventoryManager.Database.GetAllItems())
		{
			if (itemData == null || !itemData.ShowInBuySellWindow) continue;

			TreeItem subItem = itemTreeUI.CreateItem(root);
			treeItems[itemData.ItemID] = subItem;
			subItem.SetText(0, $"{itemData.ItemName}  (${itemData.buyPrice:N0}/${itemData.sellPrice:N0})");
			subItem.SetText(2, GetOwnedCount(teamBase, itemData).ToString());
			subItem.AddButton(1, buyTexture, itemData.ItemID);
			subItem.AddButton(3, sellTexture, itemData.ItemID);
		}

		RefreshTransactionSummary();
	}

	private void UpdateDisplayedQuantity(int itemId)
	{
		if (!treeItems.TryGetValue(itemId, out TreeItem treeItem)) return;
		ItemData itemData = InventoryManager.Instance.GetItemData(itemId);
		if (itemData == null) return;

		int quantity = GetOwnedCount(GameManager.Instance.currentBase, itemData)
		               + currentItemChange.GetValueOrDefault(itemId, 0);
		treeItem.SetText(2, quantity.ToString());
	}

	private static int GetOwnedCount(TeamBaseCellDefinition teamBase, ItemData itemData)
	{
		if (teamBase == null || itemData == null) return 0;
		if (itemData is Craft) return teamBase.GetCraftCountForItem(itemData.ItemID);
		return teamBase.GetItemCounts.GetValueOrDefault(itemData.ItemID, 0);
	}
}
