using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridObjectInventory : GridObjectNode, IContextUser<GridObjectNode>
{
	
	[Export]
	protected Godot.Collections.Array<Enums.InventoryType> inventoryTypes = new Godot.Collections.Array<Enums.InventoryType>();

	private System.Collections.Generic.Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids =
		new System.Collections.Generic.Dictionary<Enums.InventoryType, InventoryGrid>();

	protected override void Setup()
	{
		GD.Print("Inventory Setup");
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager == null)
		{
			GD.Print("Error: InventoryManager not found");
			return;
		}

		// Enums.InventoryType randomType = inventoryTypes.PickRandom();
		foreach (Enums.InventoryType inventoryType in inventoryTypes)
		{
			InventoryGrid inventory = inventoryManager.GetInventoryGrid(inventoryType);
			if (inventory == null)
			{
				GD.Print("Error: inventory not found");
				continue;
			}
			inventoryGrids.Add(inventoryType, inventory);

			if (inventoryType == Enums.InventoryType.LeftHand)
			{
				GD.Print($" test adding Item to {Enum.GetName(typeof(Enums.InventoryType), Enums.InventoryType.LeftHand)}: " +
				         $"{inventory.TryAddItem(InventoryManager.Instance.GetRandomItem(),1)} ");
			}
		}	
	}
	
	public bool TryGetInventory(Enums.InventoryType inventoryType, out InventoryGrid inventory)
	{
		inventory = null;
		if (inventoryGrids == null) return false;
		if (!inventoryGrids.ContainsKey(inventoryType)) return false;
		
		inventory = inventoryGrids[inventoryType];
		return true;
		
	}

	public Dictionary<string, Callable> GetContextActions()
	{
		Dictionary<string, Callable> actions = new Dictionary<string, Callable>();
		foreach (var inventoryPair in inventoryGrids)
		{
			if (inventoryPair.Value == null) continue;
			if(!inventoryPair.Value.InventorySettings.HasFlag(Enums.InventorySettings.IsEquipmentinventory)) continue;
			
			if(inventoryPair.Value.ItemCount < 1) continue;
			
			List<Item> items = inventoryPair.Value.UniqueItems.Select(i =>i.item).ToList();

			foreach (var item in items)
			{
				System.Collections.Generic.Dictionary<String, Callable> itemCallables = new System.Collections.Generic.Dictionary<string, Callable>();
				foreach (var c in itemCallables)
					actions.Add(c.Key, c.Value);
			}
		}
		
		return actions;
	}

	public GridObjectNode parent { get; set; }
}
