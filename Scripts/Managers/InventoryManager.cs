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
	
	[Export] public ItemDatabase Database;
	[Export] Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = new Dictionary<Enums.InventoryType, InventoryGrid>();
	[Export] Array<ItemData> itemDatas = new Array<ItemData>();
	
	
	Dictionary<Enums.InventoryType, InventoryGridUI> runtimeInventoryGridUIs = new Dictionary<Enums.InventoryType, InventoryGridUI>();
	[Export]public MouseHeldInventoryUI  mouseHeldInventoryUI {get; protected set;}
	
	[Export]public PackedScene InventorySlotPrefab{ get; protected set;}
	[Export]public PackedScene BlankSlotPrefab{ get; protected set;}
	[Export]public StartingEuipmentUI StartingEuipmentUi { get; protected set;}

	[Export] public Dictionary<ItemData, int> startingItems = new();
	public override string GetManagerName() => "InventoryManager";

	protected override async Task _Setup(bool loadingData)
	{
		InventoryGrid[] grids = NodeExtensions.LoadFilesOfTypeFromDirectory("res://Data/InventoryGrids/", "InventoryGrid")
			.Cast<InventoryGrid>().ToArray();

		inventoryGrids.Clear();
		foreach (InventoryGrid inventoryGrid in grids)
		{
			if(inventoryGrid != null)
				inventoryGrids.Add(inventoryGrid.InventoryType, inventoryGrid);
		}
    
		await Task.CompletedTask;
	}


	protected override async Task _Execute(bool loadingData)
	{
		StartingEuipmentUi.ShowCall();
		await ToSignal(StartingEuipmentUi.acceptButton, BaseButton.SignalName.Pressed);
		StartingEuipmentUi.HideCall();
		
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
	
	
	public ItemData GetItemData(int itemID)
	{
		if (Database != null && Database.Items.ContainsKey(itemID))
		{
			return Database.Items[itemID];
		}
		return null;
	}
	#region manager Data
	public override void Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		base.Load(data);
		if(!HasLoadedData) return;
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