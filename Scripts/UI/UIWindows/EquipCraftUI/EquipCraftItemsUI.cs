using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class EquipCraftItemsUI : UIWindow
{
	[Export] private ItemList onCraftList;
	[Export] private ItemList atBaseList;
	[Export] private Button addButton;
	[Export] private Button removeButton;

	public Craft currentCraft;

	private TeamBaseCellDefinition CurrentBase => GameManager.Instance.currentBase;

	protected override Task _Setup()
	{
		if (addButton != null)
			addButton.Pressed += AddButtonOnPressed;

		if (removeButton != null)
			removeButton.Pressed += RemoveButtonOnPressed;

		return base._Setup();
	}

	private void AddButtonOnPressed()
	{
		if (atBaseList == null || !atBaseList.IsAnythingSelected()) return;
		if (currentCraft == null || CurrentBase == null) return;

		bool moveEntireStack = Input.IsKeyPressed(Key.Shift);
		bool changed = false;
		List<int> selectedItemIds = GetSelectedItemIds(atBaseList);
		foreach (int itemId in selectedItemIds)
		{
			int availableCount = CurrentBase.GetItemCounts.GetValueOrDefault(itemId, 0);
			int transferCount = moveEntireStack ? availableCount : Mathf.Min(1, availableCount);
			if (transferCount <= 0 || !CurrentBase.TryRemoveItem(itemId, transferCount))
				continue;

			if (currentCraft.TryAddItem(itemId, transferCount))
			{
				changed = true;
				continue;
			}

			// Keep the stack at the base if the craft rejects it.
			CurrentBase.TryAddItem(itemId, transferCount);
		}

		RefreshAfterTransfer(
			changed,
			selectedItemIds.Count > 0 ? selectedItemIds[0] : -1,
			true
		);
	}

	private void RemoveButtonOnPressed()
	{
		if (onCraftList == null || !onCraftList.IsAnythingSelected()) return;
		if (currentCraft == null || CurrentBase == null) return;

		bool moveEntireStack = Input.IsKeyPressed(Key.Shift);
		bool changed = false;
		List<int> selectedItemIds = GetSelectedItemIds(onCraftList);
		foreach (int itemId in selectedItemIds)
		{
			int availableCount = currentCraft.GetItemCounts.GetValueOrDefault(itemId, 0);
			int transferCount = moveEntireStack ? availableCount : Mathf.Min(1, availableCount);
			if (transferCount <= 0 || !currentCraft.TryRemoveItem(itemId, transferCount))
				continue;

			if (CurrentBase.TryAddItem(itemId, transferCount))
			{
				changed = true;
				continue;
			}

			// Keep the stack on the craft if the base rejects it.
			currentCraft.TryAddItem(itemId, transferCount);
		}

		RefreshAfterTransfer(
			changed,
			selectedItemIds.Count > 0 ? selectedItemIds[0] : -1,
			false
		);
	}

	protected override Task DrawUI()
	{
		DrawItemLists();
		return Task.CompletedTask;
	}

	private void DrawItemLists(int selectedItemId = -1, bool selectAtBase = false)
	{
		onCraftList?.Clear();
		atBaseList?.Clear();

		if (onCraftList == null || atBaseList == null ||
		    currentCraft == null || CurrentBase == null)
		{
			UpdateButtonStates();
			return;
		}

		DrawStacks(
			onCraftList,
			currentCraft.GetItemCounts,
			selectAtBase ? -1 : selectedItemId
		);
		DrawStacks(
			atBaseList,
			CurrentBase.GetItemCounts,
			selectAtBase ? selectedItemId : -1
		);
		UpdateButtonStates();
	}

	private static void DrawStacks(
		ItemList itemList,
		Godot.Collections.Dictionary<int, int> itemCounts,
		int selectedItemId)
	{
		foreach (KeyValuePair<int, int> pair in itemCounts)
		{
			if (pair.Value <= 0) continue;

			ItemData itemData = InventoryManager.Instance?.GetItemData(pair.Key);
			if (itemData == null || itemData is Craft) continue;

			int listIndex = itemList.AddItem(
				$"{itemData.ItemName}  x{pair.Value:N0}",
				itemData.ItemIcon
			);
			itemList.SetItemMetadata(listIndex, pair.Key);
			if (pair.Key == selectedItemId) itemList.Select(listIndex);
		}
	}

	private static List<int> GetSelectedItemIds(ItemList itemList)
	{
		List<int> itemIds = new();
		foreach (int selectedIndex in itemList.GetSelectedItems())
		{
			int itemId = itemList.GetItemMetadata(selectedIndex).AsInt32();
			if (!itemIds.Contains(itemId)) itemIds.Add(itemId);
		}

		return itemIds;
	}

	private void RefreshAfterTransfer(
		bool changed,
		int selectedItemId,
		bool selectAtBase)
	{
		if (changed && !GameManager.Instance.SyncCurrentBaseToGlobeState())
			GD.PrintErr("Item transfer completed locally, but the globe transition state could not be updated.");

		DrawItemLists(selectedItemId, selectAtBase);
	}

	private void UpdateButtonStates()
	{
		if (addButton != null)
			addButton.Disabled = currentCraft == null || CurrentBase == null ||
			                     CurrentBase.GetItemCounts.Count == 0;

		if (removeButton != null)
			removeButton.Disabled = currentCraft == null || CurrentBase == null ||
			                        currentCraft.GetItemCounts.Count == 0;
	}
}
