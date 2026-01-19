using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.UI;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

public partial class MainInventoryWindow : UIWindow
{
	[Export] private Array<InventoryGridUI> inventories = new Array<InventoryGridUI>();


	protected override Task _Setup()
	{
		
		GridObjectTeamHolder playerTeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);

		if (playerTeamHolder != null)
		{
			playerTeamHolder.SelectedGridObjectChanged += PlayerTeamHolderOnSelectedGridObjectChanged;
		}
		return base._Setup();
	}

	private void PlayerTeamHolderOnSelectedGridObjectChanged(GridObject gridObject)
	{
		if (gridObject != null)
		{
			if(!gridObject.TryGetGridObjectNode<GridObjectInventory>(out var gridObjectInventory)) return;
			
			SetupInventoryRefreences(gridObjectInventory);
		}
	}

	private void SetupInventoryRefreences(GridObjectInventory gridObjectInventory)
	{

		if (gridObjectInventory == null) return;
		foreach (InventoryGridUI inventoryUI in inventories)
		{
			if (!gridObjectInventory.TryGetInventory(inventoryUI.inventoryType, out var inventoryRef))
			{
				continue;
			}
			inventoryUI.SetupInventoryUI(inventoryRef);
		}
	}
}
