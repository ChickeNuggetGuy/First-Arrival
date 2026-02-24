using System.Linq;
using Godot;
using Godot.Collections;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.UI;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class StartingEuipmentUI : UIWindow
{
	[Export] Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = new();
	
	Dictionary<Enums.InventoryType,InventoryGridUI> inventoryGridUIs = new Dictionary<Enums.InventoryType, InventoryGridUI>();
	

	[Export] public Label unitNameLabel;
	[Export] public Button acceptButton;
	[Export] public Button previousButton;
	[Export] public Button nextButton;

	[ExportGroup("Grid Object Stat Settings")] 
	[Export] protected Array<TextStatBar> _statBars = new();

	private Array<GridObject> playerUnits = new Array<GridObject>();
	private int currentUnitIndex = 0;

	protected override Task _Setup()
	{
		InventoryManager inventoryManager = InventoryManager.Instance;
		if(inventoryManager == null)
		{
			GD.PrintErr("inventoryManager == null");
			return Task.CompletedTask;
		}

		if (inventoryManager.startingItems == null || inventoryManager.startingItems.Count == 0)
		{
			GD.PrintErr("startingItems == null");
			return Task.CompletedTask;
		}

		foreach (var inventoryGridKVP in inventoryGrids)
		{
			InventoryGrid inventoryGrid = inventoryGridKVP.Value == null? 	inventoryManager.GetInventoryGrid(inventoryGridKVP.Key) : inventoryGridKVP.Value;
			
			if (inventoryGrid == null)
			{
				GD.PrintErr("inventoryGrid == null");
				continue;
			}
			
			inventoryGrid.Initialize();
			inventoryGrid.ClearInventory();
		}

		inventoryGridUIs.Clear();
		if (this.TryGetAllComponentsInChildrenRecursive<InventoryGridUI>(out var inventoryGridUIList))
		{
			foreach (var inventoryGridUI in inventoryGridUIList)
			{
				inventoryGridUIs.Add(inventoryGridUI.inventoryType ,inventoryGridUI);
			}
		}

		previousButton.Pressed += PreviousUnit;
		nextButton.Pressed += NextUnit;

		return base._Setup();
	}

	private void InitializePlayerUnits()
	{
		playerUnits.Clear();
		var teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (teamHolder != null && teamHolder.GridObjects.ContainsKey(Enums.GridObjectState.Active))
		{
			playerUnits.AddRange(teamHolder.GridObjects[Enums.GridObjectState.Active]);
		}
		currentUnitIndex = 0;
	}

	private void NextUnit()
	{
		if (playerUnits.Count == 0) return;
		currentUnitIndex = (currentUnitIndex + 1) % playerUnits.Count;
		UpdateUnitInventory();
	}

	private void PreviousUnit()
	{
		if (playerUnits.Count == 0) return;
		currentUnitIndex = (currentUnitIndex - 1 + playerUnits.Count) % playerUnits.Count;
		UpdateUnitInventory();
	}

	private void UpdateUnitInventory()
	{
		if (playerUnits.Count == 0) return;
		
		GridObject currentUnit = playerUnits[currentUnitIndex];
		if (currentUnit == null) return;
		
		unitNameLabel.Text = currentUnit.Name;

		if (!currentUnit.TryGetGridObjectNode<GridObjectInventory>(out var gridObjectInventory))
		{
			GD.PrintErr($"Unit {currentUnit.Name} has no GridObjectInventory component");
			return;
		}

		foreach (var pair in inventoryGridUIs)
		{
			// Skip Ground inventory, it stays constant as the starting items pool
			if (pair.Key == Enums.InventoryType.Ground) continue;

			if (gridObjectInventory.TryGetInventory(pair.Key, out var unitInventory))
			{
				pair.Value.SetInventroyGrid(unitInventory);
				pair.Value.AutoFetchGround = false;
				pair.Value.SetupInventoryUI(unitInventory);
			}
			else
			{
				// Unit might not have this inventory type (e.g. some units might not have a backpack)
				GD.Print($"Unit {currentUnit.Name} missing inventory type {pair.Key}");
			}
		}

		if (_statBars != null && _statBars.Count > 0)
		{
			currentUnit.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder);
			foreach (var statBar in _statBars)
			{
				statBar.UpdateStat(statHolder);
			}
		}
	}

	private void populateInventoryGrid(Enums.InventoryType inventoryType, 
		Godot.Collections.Dictionary<ItemData, int> items, InventoryManager inventoryManager)
	{
		if (!inventoryGrids.ContainsKey(inventoryType)) return;
		InventoryGrid grid = inventoryGrids[inventoryType];
		
		if (grid == null) return;

		Array<Item> itemsNotAdded = new();
		foreach (var itemDataKVP in items)
		{
			Item item = inventoryManager.InstantiateItem(itemDataKVP.Key);
			if (!grid.TryAddItem(item, itemDataKVP.Value))
			{
				itemsNotAdded.Add(item);
			}
		}

		if (itemsNotAdded.Count > 0)
		{
			GD.Print("Cound not add " + itemsNotAdded.Count + " items!");

			foreach (Item item in itemsNotAdded)
			{
				item.QueueFree();
			}
		}

	}

	protected override void _Show()
	{
		InitializePlayerUnits();
		InventoryManager inventoryManager = InventoryManager.Instance;
		populateInventoryGrid(Enums.InventoryType.Ground, inventoryManager.startingItems, inventoryManager);
	
		foreach (var pair in inventoryGridUIs)
		{
			if (pair.Key == Enums.InventoryType.Ground)
			{
				if (!inventoryGrids.ContainsKey(pair.Key))
				{
					GD.Print("InventoryGrid not found: " + pair.Key);
					continue;
				}

				pair.Value.SetInventroyGrid(inventoryGrids[pair.Key]);
				pair.Value.AutoFetchGround = false;
				pair.Value.ShowCall();
			}
		}

		UpdateUnitInventory();
		base._Show();
	}
}
