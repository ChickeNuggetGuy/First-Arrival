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
	public GridObjectNode parent { get; set; }
	
	[Export]
	protected Godot.Collections.Array<Enums.InventoryType> inventoryTypes = new();

	private Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = new();

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
				GD.Print($"Error: inventory type {inventoryType}  not found");
				continue;
			}
			inventoryGrids.Add(inventoryType, inventory);
			
			
			inventory.ItemAdded += InventoryOnItemAdded;
			inventory.ItemRemoved += InventoryOnItemRemoved;
			// if (inventoryType == Enums.InventoryType.LeftHand)
			// {
			// 	GD.Print($" test adding Item to {Enum.GetName(typeof(Enums.InventoryType), Enums.InventoryType.LeftHand)}: " +
			// 	         $"{inventory.TryAddItem(InventoryManager.Instance.GetRandomItem(),1)} ");
			// }
		}	
	}

	private void InventoryOnItemRemoved(InventoryGrid inventoryGrid, Item itemRemoved)
	{
		if (parentGridObject.TryGetGridObjectNode<GridObjectAnimation>(out var gridObjectAnimation))
		{
			if (inventoryGrid.InventorySettings.HasFlag(Enums.InventorySettings.IsEquipmentinventory))
			{
				//Item was equipped. Determine of the item was a weapon.
				if (itemRemoved.ItemData.ActionDefinitions.Any(definition => definition is RangedAttackActionDefinition))
				{
					//Ranged Weapon
					gridObjectAnimation.RemoveWeaponState(Enums.WeaponState.Ranged);
				}
				else if (itemRemoved.ItemData.ActionDefinitions.Any(definition => definition is MeleeAttackActionDefinition))
				{
					//Melee Weapon
					gridObjectAnimation.RemoveWeaponState(Enums.WeaponState.Melee);
				}
				
				//Hide Visual
				if (inventoryGrid.InventoryType == Enums.InventoryType.LeftHand)
				{
					itemRemoved.HideVisual(parentGridObject.LeftHandBoneAttachment);
				}
				else if (inventoryGrid.InventoryType == Enums.InventoryType.RightHand)
				{
					itemRemoved.HideVisual(parentGridObject.RightHandBoneAttachment);
				}
			}
		}
	}

	private void InventoryOnItemAdded(InventoryGrid inventoryGrid, Item itemAdded)
	{
		if (parentGridObject.TryGetGridObjectNode<GridObjectAnimation>(out var gridObjectAnimation))
		{
			if (inventoryGrid.InventorySettings.HasFlag(Enums.InventorySettings.IsEquipmentinventory))
			{
				//Item was equipped. Determine of the item was a weapon.
				if (itemAdded.ItemData.ActionDefinitions.Any(definition => definition is RangedAttackActionDefinition))
				{
					//Ranged Weapon
					gridObjectAnimation.AddWeaponState(Enums.WeaponState.Ranged);
				}
				else if (itemAdded.ItemData.ActionDefinitions.Any(definition => definition is MeleeAttackActionDefinition))
				{
					//Melee Weapon
					gridObjectAnimation.AddWeaponState(Enums.WeaponState.Melee);
				}

				//Show Visual
				if (inventoryGrid.InventoryType == Enums.InventoryType.LeftHand)
				{
					itemAdded.ShowVisual(parentGridObject.LeftHandBoneAttachment);
				}
				else if (inventoryGrid.InventoryType == Enums.InventoryType.RightHand)
				{
					itemAdded.ShowVisual(parentGridObject.RightHandBoneAttachment);
				}
				
				
			
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

	
	
	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>();
		
		// Save the inventory types this grid object has
		var inventoryTypesArray = new Godot.Collections.Array<int>();
		foreach (var type in inventoryTypes)
		{
			inventoryTypesArray.Add((int)type);
		}
		data.Add("inventory_types", inventoryTypesArray);
		
		// Save each inventory's contents
		var inventoriesData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var kvp in inventoryGrids)
		{
			var inventoryType = kvp.Key;
			var inventoryGrid = kvp.Value;
			
			var inventoryData = new Godot.Collections.Dictionary<string, Variant>();
			
			// Save items in the inventory
			var itemsData = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
			for (int x = 0; x < inventoryGrid.Items.GetLength(0); x++)
			{
				for (int y = 0; y < inventoryGrid.Items.GetLength(1); y++)
				{
					var itemData = inventoryGrid.Items[x, y];
					if (itemData.item != null)
					{
						var itemEntry = new Godot.Collections.Dictionary<string, Variant>();
						itemEntry.Add("x", x);
						itemEntry.Add("y", y);
						itemEntry.Add("count", itemData.count);
						itemEntry.Add("item_name", itemData.item.ItemData.ItemName);
						itemEntry.Add("item_id", itemData.item.ItemData.ItemID);
						itemsData.Add(itemEntry);
					}
				}
			}
			
			inventoryData.Add("items", itemsData);
			inventoriesData.Add(inventoryType.ToString(), inventoryData);
		}
		
		data.Add("inventories", inventoriesData);
		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		// Clear existing inventories
		inventoryGrids.Clear();
		inventoryTypes.Clear();
		
		// Load inventory types
		if (data.ContainsKey("inventory_types"))
		{
			var inventoryTypesArray = (Godot.Collections.Array<int>)data["inventory_types"];
			foreach (int typeValue in inventoryTypesArray)
			{
				var inventoryType = (Enums.InventoryType)typeValue;
				inventoryTypes.Add(inventoryType);
			}
		}
		
		// Re-setup inventories from types
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager != null)
		{
			foreach (Enums.InventoryType inventoryType in inventoryTypes)
			{
				InventoryGrid inventory = inventoryManager.GetInventoryGrid(inventoryType);
				if (inventory != null)
				{
					inventoryGrids.Add(inventoryType, inventory);
				}
			}
		}
		
		// Load inventory contents
		if (data.ContainsKey("inventories"))
		{
			var inventoriesData = (Godot.Collections.Dictionary<string, Variant>)data["inventories"];
			
			foreach (var inventoryEntry in inventoriesData)
			{
				var inventoryType = (Enums.InventoryType)Enum.Parse(typeof(Enums.InventoryType), inventoryEntry.Key);
				var inventoryData = (Godot.Collections.Dictionary<string, Variant>)inventoryEntry.Value;
				
				if (inventoryGrids.TryGetValue(inventoryType, out var inventoryGrid) && 
				    inventoryData.ContainsKey("items"))
				{
					var itemsData = (Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>)inventoryData["items"];
					
					foreach (var itemEntry in itemsData)
					{
						int x = (int)itemEntry["x"];
						int y = (int)itemEntry["y"];
						int count = (int)itemEntry["count"];
						var itemData = InventoryManager.Instance.GetItemData(itemEntry["item_id"].AsInt32());
							
						if (itemData != null)
						{
							Item item = ItemData.CreateItem(itemData);
							if (item != null)
							{
								// Directly set the item in the grid
								inventoryGrid.Items[x, y] = (item, count);
								item.currentGrid = inventoryGrid;
							}
						}
					}
					
					// Mark cache as dirty since we've modified items directly
					var uniqueItemsField = inventoryGrid.GetType().GetField("_uniqueItemsCache", 
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					var isCacheDirtyField = inventoryGrid.GetType().GetField("_isCacheDirty", 
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					
					if (uniqueItemsField != null) uniqueItemsField.SetValue(inventoryGrid, null);
					if (isCacheDirtyField != null) isCacheDirtyField.SetValue(inventoryGrid, true);
				}
			}
		}
	}
}