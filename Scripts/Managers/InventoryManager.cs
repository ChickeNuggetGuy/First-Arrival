using System;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.UI;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

public partial class InventoryManager : Manager<InventoryManager>
{
	
	public ItemDatabase Database;
	[Export] Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = new();
	
	
	Dictionary<Enums.InventoryType, InventoryGridUI> runtimeInventoryGridUIs = new();

	
	public PackedScene InventorySlotPrefab{ get; protected set;}
	public PackedScene BlankSlotPrefab{ get; protected set;}

	public Dictionary<ItemData, int> startingItems = new();
	public override string GetManagerName() => "InventoryManager";

	protected override async Task _Setup(bool loadingData)
	{
		Database = ResourceLoader.Load<ItemDatabase>("res://Data/InventorySystem/ItemsDatabase.tres");
		InventorySlotPrefab = ResourceLoader.Load<PackedScene>("res://Scenes/UI/item_slot_ui.tscn");
		BlankSlotPrefab = ResourceLoader.Load<PackedScene>("res://Scenes/UI/blank_slot_ui.tscn");
		InventoryGrid[] grids = NodeExtensions.LoadFilesOfTypeFromDirectory("res://Data/InventoryGrids/", "InventoryGrid")
			.Cast<InventoryGrid>().ToArray();

		inventoryGrids.Clear();
		foreach (InventoryGrid inventoryGrid in grids)
		{
			if(inventoryGrid != null)
				inventoryGrids.Add(inventoryGrid.InventoryType, inventoryGrid);
		}


		if (startingItems.Count == 0)
			ResetStartingItemsToDefaults();
		await Task.CompletedTask;
	}


	protected override async Task _Execute(bool loadingData)
	{
		await Task.CompletedTask;
	}

	public void TeamHolderOnSelectedGridObjectChanged(GridObject gridObject)
	{
		if (!gridObject.TryGetGridObjectNode<GridObjectInventory>(out var gridObjectInventory)) return;
		
		//Refresh all Grid objects inventories
		foreach (var runtimeInventoryGridUI in runtimeInventoryGridUIs)
		{
			if(gridObjectInventory.TryGetInventory(runtimeInventoryGridUI.Key, out var inventoryGrid))
				runtimeInventoryGridUI.Value.SetupInventoryUI(inventoryGrid);
		}
		
		//Refresh Ground inventory at gridObjects Position
		if (!runtimeInventoryGridUIs.ContainsKey(Enums.InventoryType.Ground))
		{
			GD.Print("Ground Inventory not added");return;
		}
		else
		{

			runtimeInventoryGridUIs[Enums.InventoryType.Ground].SetupInventoryUI(gridObject.GridPositionData.AnchorCell.InventoryGrid);
		}
	}

	public void AddRuntimeInventoryGridUI(Enums.InventoryType type, InventoryGridUI gridUi)
	{
		if (gridUi == null) return;

		if (runtimeInventoryGridUIs.TryGetValue(type, out InventoryGridUI existing))
		{
			if (existing == gridUi) return;
			if (existing != null && GodotObject.IsInstanceValid(existing)) return;
			runtimeInventoryGridUIs[type] = gridUi;
			return;
		}

		runtimeInventoryGridUIs.Add(type, gridUi);
	}

	public void RemoveRuntimeInventoryGridUI(
		Enums.InventoryType type,
		InventoryGridUI gridUi)
	{
		if (runtimeInventoryGridUIs.TryGetValue(type, out InventoryGridUI registered) &&
		    registered == gridUi)
		{
			runtimeInventoryGridUIs.Remove(type);
		}
	}


	public InventoryGrid GetInventoryGrid(Enums.InventoryType type)
	{
		if (!inventoryGrids.ContainsKey(type)) return null;
		InventoryGrid inventoryGrid = inventoryGrids[type].Duplicate() as InventoryGrid;
		inventoryGrid.Initialize();
		return inventoryGrid;
	}
	public Item InstantiateItem(ItemData itemData)
	{
		if (itemData == null) return null;
		return ItemData.CreateItem(itemData);
	}

	public Item GetRandomItem()
	{
		if(Database == null) return null;
		if(Database.Items.Count ==0) return null;
		Array<ItemData> items = Database.Items.Values.Where(itemData => itemData != null && 
		                                                                !itemData.globeOnly) as Array<ItemData>;
		
		if(items == null) return null;
		return ItemData.CreateItem(items.PickRandom()); 
	}
	
	
	public ItemData GetItemData(int itemID)
	{
		if (Database != null && Database.Items.ContainsKey(itemID))
		{
			return Database.Items[itemID];
		}
		return null;
	}

	public void SetStartingItems(Dictionary<int, int> itemCounts)
	{
		startingItems.Clear();
		if (itemCounts == null || Database == null) return;

		foreach (var pair in itemCounts)
		{
			ItemData itemData = GetItemData(pair.Key);
			if (itemData == null || itemData is Craft || pair.Value <= 0) continue;
			startingItems[itemData] = pair.Value;
		}
	}

	public void ResetStartingItemsToDefaults()
	{
		startingItems.Clear();
		if (Database == null) return;

		foreach (ItemData itemData in Database.GetAllItems())
		{
			if (itemData != null && itemData is not Craft)
				startingItems[itemData] = 4;
		}
	}
	
	#region manager Data
	public override Task Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		if(!HasLoadedData)  return Task.CompletedTask;
		return Task.CompletedTask;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		return null;
	}
	#endregion
	
	public override void Deinitialize()
	{
		return;
	}
}
