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

	Dictionary<Enums.InventoryType, InventoryGridUI> inventoryGridUIs =
		new Dictionary<Enums.InventoryType, InventoryGridUI>();

	[Export] public LoadingScreenUI loadingSCcreenUI;
	[Export] public Label unitNameLabel;
	[Export] public Button acceptButton;
	[Export] public Button previousButton;
	[Export] public Button nextButton;

	[ExportGroup("Grid Object Stat Settings")] [Export]
	protected Array<TextStatBar> _statBars = new();

	private Array<GridObject> playerUnits = new Array<GridObject>();
	private int currentUnitIndex = 0;

	[Signal]
	public delegate void AcceptPressedEventHandler();

	protected override Task _Setup()
	{
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager == null)
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
			InventoryGrid inventoryGrid = inventoryGridKVP.Value == null
				? inventoryManager.GetInventoryGrid(inventoryGridKVP.Key)
				: inventoryGridKVP.Value;

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
				inventoryGridUIs.Add(inventoryGridUI.inventoryType, inventoryGridUI);
			}
		}

		acceptButton.Pressed += AcceptButtonOnPressed;
		previousButton.Pressed += PreviousUnit;
		nextButton.Pressed += NextUnit;

		return base._Setup();
	}

	private void AcceptButtonOnPressed()
	{
		EmitSignal(SignalName.AcceptPressed);
		_ = HideCall();
	}

	private void InitializePlayerUnits()
	{
		playerUnits.Clear();
		var teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (teamHolder != null &&
		    teamHolder.GridObjects.TryGetValue(Enums.GridObjectState.Active, out var activeGridObjects))
		{
			playerUnits.AddRange(activeGridObjects);
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

		if (_statBars is { Count: > 0 })
		{
			if(currentUnit.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder))
			{
				foreach (var statBar in _statBars)
				{

					statBar.UpdateStat(statHolder);
				}
			}
		}
	}

	
	
	protected override async Task DrawUI()
	{
		MouseFilter = MouseFilterEnum.Stop;
		InitializePlayerUnits();

		InventoryManager inventoryManager = InventoryManager.Instance;
		populateInventoryGrid(Enums.InventoryType.Ground, inventoryManager.startingItems, inventoryManager);

		foreach (var pair in inventoryGridUIs)
		{
			// Safe-catch if Ground wasn't assigned in the Godot Editor Inspector
			if (!inventoryGrids.ContainsKey(pair.Key))
			{
				InventoryGrid groundGrid = inventoryManager.GetInventoryGrid(pair.Key);
				if (groundGrid != null)
				{
					inventoryGrids.Add(pair.Key, groundGrid);
				}
				else
				{
					GD.PrintErr($"InventoryGrid not found globally or locally: {pair.Key}");
					continue;
				}
			}

			pair.Value.SetInventroyGrid(inventoryGrids[pair.Key]);
			pair.Value.AutoFetchGround = false;
			await pair.Value.ShowCall();
		}

		UpdateUnitInventory();
		await loadingSCcreenUI.HideCall();
		base._Show();
	}


	override protected void _Hide()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}
	
	private void populateInventoryGrid(Enums.InventoryType inventoryType,
		Godot.Collections.Dictionary<ItemData, int> items, InventoryManager inventoryManager)
	{
		// Dynamically fetch from the manager if the dictionary is missing the key
		if (!inventoryGrids.ContainsKey(inventoryType))
		{
			InventoryGrid fallbackGrid = inventoryManager.GetInventoryGrid(inventoryType);
			if (fallbackGrid != null)
				inventoryGrids.Add(inventoryType, fallbackGrid);
			else
				return;
		}

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
			GD.Print($"Could not add {itemsNotAdded.Count} items to {inventoryType} grid!");

			foreach (Item item in itemsNotAdded)
			{
				item.QueueFree();
			}
		}
	}
}