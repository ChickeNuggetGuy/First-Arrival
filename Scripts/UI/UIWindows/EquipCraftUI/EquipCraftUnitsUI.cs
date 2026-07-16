using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot.Collections;

[GlobalClass]
public partial class EquipCraftUnitsUI : UIWindow
{

	[Export] private ItemList onCraftList;

	[Export] private ItemList atBaseList;

	[Export] private Button addButton;
	[Export] private Button removeButton;
	
	[Export] private EquipCraftUI equipCraftUI;

	public Craft currentCraft
	{
		get => equipCraftUI?.currentCraft;
	}
	
	public TeamBaseCellDefinition currentBase
	{
		get => GameManager.Instance.currentBase;
	}

	protected override Task _Setup()
	{
		if (addButton != null)
		{
			addButton.Pressed += AddButtonOnPressed;
		}

		if (removeButton != null)
		{
			removeButton.Pressed += RemoveButtonOnPressed;
		}
		return base._Setup();
	}

	private void AddButtonOnPressed()
	{
		if (atBaseList == null || !atBaseList.IsAnythingSelected()) return;
		if (currentCraft == null || currentBase == null) return;

		List<GridObject> selectedUnits = GetSelectedUnits(
			atBaseList,
			currentBase.GetStationedGridObjects()
		);
		bool changed = false;
		foreach (GridObject unit in selectedUnits)
		{
			if (!currentBase.TryRemoveStationedGridObject(unit)) continue;
			if (currentCraft.TryAddStationedGridObject(unit))
			{
				changed = true;
				continue;
			}

			// Keep ownership consistent if the craft rejects the unit.
			currentBase.TryAddStationedGridObject(unit);
		}

		RefreshAfterTransfer(changed);
	}
	
	private void RemoveButtonOnPressed()
	{
		if (onCraftList == null || !onCraftList.IsAnythingSelected()) return;
		if (currentCraft == null || currentBase == null) return;

		List<GridObject> selectedUnits = GetSelectedUnits(
			onCraftList,
			currentCraft.GetStationedUnits()
		);
		bool changed = false;
		foreach (GridObject unit in selectedUnits)
		{
			if (!currentCraft.TryRemoveStationedGridObject(unit)) continue;
			if (currentBase.TryAddStationedGridObject(unit))
			{
				changed = true;
				continue;
			}

			// Keep ownership consistent if the base rejects the unit.
			currentCraft.TryAddStationedGridObject(unit);
		}

		RefreshAfterTransfer(changed);
	}


	protected override Task DrawUI()
	{
		DrawItemLists();
		return Task.CompletedTask;
	}

	private void DrawItemLists()
	{
		onCraftList?.Clear();
		atBaseList?.Clear();

		Craft craft = currentCraft;
		TeamBaseCellDefinition teamBase = currentBase;
		if (craft == null || teamBase == null)
		{
			UpdateButtonStates();
			return;
		}

		// Draw units on the selected craft.
		onCraftList.Clear();

		Array<GridObject> unitsOnBoard = craft.GetStationedUnits();
		for (int i = 0; i < unitsOnBoard.Count; i++)
		{
			GridObject unit = unitsOnBoard[i];
			if (unit == null) continue;
			int listIndex = onCraftList.AddItem(unit.GetName());
			onCraftList.SetItemMetadata(listIndex, i);
		}

		// Draw units currently available at the base.
		atBaseList.Clear();

		Array<GridObject> unitsInBase = teamBase.GetStationedGridObjects();
		for (int i = 0; i < unitsInBase.Count; i++)
		{
			GridObject unit = unitsInBase[i];
			if (unit == null) continue;
			int listIndex = atBaseList.AddItem(unit.GetName());
			atBaseList.SetItemMetadata(listIndex, i);
		}

		UpdateButtonStates();
	}

	private static List<GridObject> GetSelectedUnits(
		ItemList itemList,
		Array<GridObject> source)
	{
		List<GridObject> selectedUnits = new();
		foreach (int selectedIndex in itemList.GetSelectedItems())
		{
			int sourceIndex = itemList.GetItemMetadata(selectedIndex).AsInt32();
			if (sourceIndex < 0 || sourceIndex >= source.Count) continue;

			GridObject unit = source[sourceIndex];
			if (unit != null && !selectedUnits.Contains(unit))
				selectedUnits.Add(unit);
		}

		return selectedUnits;
	}

	private void RefreshAfterTransfer(bool changed)
	{
		if (changed && !GameManager.Instance.SyncCurrentBaseToGlobeState())
			GD.PrintErr("Unit transfer completed locally, but the globe transition state could not be updated.");

		DrawItemLists();
	}

	private void UpdateButtonStates()
	{
		if (addButton != null)
			addButton.Disabled = currentCraft == null || currentBase == null ||
			                     currentBase.GetStationedGridObjects().Count == 0;

		if (removeButton != null)
			removeButton.Disabled = currentCraft == null || currentBase == null ||
			                        currentCraft.GetStationedUnits().Count == 0;
	}
}
