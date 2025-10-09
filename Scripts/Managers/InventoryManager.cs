using System;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.UI;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class InventoryManager : Manager<InventoryManager>
{
	[Export] Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = new Dictionary<Enums.InventoryType, InventoryGrid>();
	[Export] Array<ItemData> itemDatas = new Array<ItemData>();
	
	
	Dictionary<Enums.InventoryType, InventoryGridUI> runtimeInventoryGridUIs = new Dictionary<Enums.InventoryType, InventoryGridUI>();
	[Export]public MouseHeldInventoryUI  mouseHeldInventoryUI {get; protected set;}
	
	[Export]public PackedScene InventorySlotPrefab{ get; protected set;}
	[Export]public PackedScene BlankSlotPrefab{ get; protected set;}
	
	[Export]public Item[] startingItems = new Item[0];
	protected override async Task _Setup()
	{
		// startingItems = new Item[GD.RandRange(0, 10)];
		//
		// for (int i = 0; i < startingItems.Length; i++)
		// {
		// 	startingItems[i] = GetRandomItem();
		// }
		return;
	}

	public void TeamHolderOnSelectedGridObjectChanged(GridObject gridObject)
	{
		//Refresh all Grid objects inventories
		foreach (var runtimeInventoryGridUI in runtimeInventoryGridUIs)
		{
			if(gridObject.TryGetInventory(runtimeInventoryGridUI.Key, out var inventoryGrid))
				runtimeInventoryGridUI.Value.SetupInventoryUI(inventoryGrid);
		}
		
		//Refresh Ground inventory at gridObjects Position
		if (!runtimeInventoryGridUIs.ContainsKey(Enums.InventoryType.Ground))
		{
			GD.Print("Ground Inventory not added");return;
		}
		else
		{
			runtimeInventoryGridUIs[Enums.InventoryType.Ground].SetupInventoryUI(gridObject.GridPositionData.GridCell.InventoryGrid);
		}
	}

	protected override async Task _Execute()
	{
		InventoryGrid[] grids = NodeExtensions.LoadFilesOfTypeFromDirectory("res://Data/InventoryGrids/","InventoryGrid").Cast<InventoryGrid>().ToArray();

		foreach (InventoryGrid inventoryGrid in grids)
		{
			inventoryGrids.Add(inventoryGrid.InventoryType, inventoryGrid);
		}
		
		ItemData[] items = NodeExtensions.LoadFilesOfTypeFromDirectory("res://Data/Items/","ItemData").Cast<ItemData>().ToArray();

		foreach (ItemData item in items)
		{
			itemDatas.Add(item);
		}
	}

	public void AddRuntimeInventoryGridUI(Enums.InventoryType type, InventoryGridUI gridUi)
	{
		if(runtimeInventoryGridUIs.ContainsKey(type)) return;
		runtimeInventoryGridUIs.Add(type, gridUi);
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
		int randIndex = GD.RandRange(0, itemDatas.Count - 1);
		return ItemData.CreateItem(itemDatas[randIndex]); 
	}
	
	#region manager Data
	protected override void GetInstanceData(ManagerData data)
	{
		GD.Print("No data to transfer");
	}

	public override ManagerData SetInstanceData()
	{
		return null;
	}
	#endregion
}