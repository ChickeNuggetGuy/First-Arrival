using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class EquipCraftUI : UIWindow
{
	[Export] private ItemList craftList;

	[Export] private Button equipmentButton;
	[Export] private Button unitButton;
	
	[Export] private EquipCraftItemsUI equipCraftItemsUI;
	[Export] private EquipCraftUnitsUI equipCraftunitsUI;
	public Craft currentCraft { get; private set; }

	protected override Task _Setup()
	{
		if (equipmentButton != null)
		{
			equipmentButton.Pressed += EquipmentButtonOnPressed;
		}

		if (unitButton != null)
		{
			unitButton.Pressed += UnitButtonOnPressed;
		}
		
		if (craftList != null)
			craftList.ItemSelected += CraftListOnItemSelected;
		UpdateActionButtons();
		return base._Setup();
		
	}

	private void CraftListOnItemSelected(long index)
	{
		int craftIndex = craftList.GetItemMetadata((int)index).AsInt32();
		TeamBaseCellDefinition currentBase = GameManager.Instance.currentBase;
		if (currentBase == null) return;

		Array<Craft> stationedCraft = currentBase.GetAllCraftData()[Enums.CraftStatus.Home];
		currentCraft = craftIndex >= 0 && craftIndex < stationedCraft.Count
			? stationedCraft[craftIndex]
			: null;
		UpdateActionButtons();
	}

	private async void UnitButtonOnPressed()
	{
		try
		{
			if (currentCraft == null || equipCraftunitsUI == null) return;
			await equipCraftunitsUI.Toggle();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to toggle equipCraftunitsUI Panel: {e.Message}\n{e.StackTrace}");
		}
	}

	private async void EquipmentButtonOnPressed()
	{
		try
		{
			if (currentCraft == null || equipCraftItemsUI == null) return;
			equipCraftItemsUI.currentCraft = currentCraft;
			await equipCraftItemsUI.Toggle();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to toggle equipCraftItemsUI Panel: {e.Message}\n{e.StackTrace}");
		}
	}

	
	protected override async Task DrawUI()
	{
		base._Show();
		DrawCraftList();
	}

	private void DrawCraftList()
	{
		currentCraft = null;
		UpdateActionButtons();
		if (craftList == null) return;
		craftList.Clear();
		TeamBaseCellDefinition currentBase = GameManager.Instance.currentBase;
		if (currentBase == null) return;

		Array<Craft> stationedCraft = currentBase.GetAllCraftData()[Enums.CraftStatus.Home];

		if (stationedCraft == null || stationedCraft.Count == 0)
		{
			return;
		}
		
		foreach (Craft craft in stationedCraft)
		{
			int index = craftList.AddItem(craft.ItemName, craft.ItemIcon);
			craftList.SetItemMetadata(index, stationedCraft.IndexOf(craft));
		}
	}

	private void UpdateActionButtons()
	{
		bool disabled = currentCraft == null;
		if (equipmentButton != null) equipmentButton.Disabled = disabled;
		if (unitButton != null) unitButton.Disabled = disabled;
	}
}
