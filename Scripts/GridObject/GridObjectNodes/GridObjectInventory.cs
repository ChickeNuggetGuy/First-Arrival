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

	public Dictionary<Enums.InventoryType, InventoryGrid> InventoryGrids { get; protected set; } = new();
	
	[Export] 
	public Godot.Collections.Array<StartingInventoryConfig> startingItemsConfig = new();
	protected override void Setup()
	{
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager == null)
		{
			GD.Print("Error: InventoryManager not found");
			return;
		}

		foreach (Enums.InventoryType inventoryType in inventoryTypes)
		{
			if (InventoryGrids.ContainsKey(inventoryType)) continue;

			InventoryGrid inventory = inventoryManager.GetInventoryGrid(inventoryType);
			if (inventory == null)
			{
				GD.Print($"Error: inventory type {inventoryType} not found");
				continue;
			}
        
			InventoryGrids.Add(inventoryType, inventory);
			inventory.ItemAdded += InventoryOnItemAdded;
			inventory.ItemRemoved += InventoryOnItemRemoved;
		}

		foreach (StartingInventoryConfig config in startingItemsConfig)
		{
			// Check if we actually have an inventory of this type initialized
			if (InventoryGrids.TryGetValue(config.InventoryType, out InventoryGrid inventory))
			{
				foreach (ItemData itemData in config.Items)
				{
					if (itemData == null) continue;
                
					Item newItem = ItemData.CreateItem(itemData);
					bool success = inventory.TryAddItem(newItem, 1);
                
					if (!success)
					{
						GD.Print($"Warning: Could not add starting item {itemData.ItemName} to {config.InventoryType} (Inventory full?)");
					}
				}
			}
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
		if (InventoryGrids == null) return false;
		if (!InventoryGrids.ContainsKey(inventoryType)) return false;
		
		inventory = InventoryGrids[inventoryType];
		return true;
		
	}

	public Dictionary<string, Callable> GetContextActions()
	{
		Dictionary<string, Callable> actions = new Dictionary<string, Callable>();
		foreach (var inventoryPair in InventoryGrids)
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
		
		var inventoryTypesArray = new Godot.Collections.Array<int>();
		foreach (var type in inventoryTypes)
		{
			if (!inventoryTypesArray.Contains((int)type))
				inventoryTypesArray.Add((int)type);
		}
		foreach (var type in InventoryGrids.Keys)
		{
			if (!inventoryTypesArray.Contains((int)type))
				inventoryTypesArray.Add((int)type);
		}
		data.Add("inventory_types", inventoryTypesArray);
		
		// Save each inventory's contents
		var inventoriesData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var kvp in InventoryGrids)
		{
			var inventoryType = kvp.Key;
			var inventoryGrid = kvp.Value;
			inventoriesData.Add(
				inventoryType.ToString(),
				inventoryGrid.SaveContents());
		}
		
		data.Add("inventories", inventoriesData);
		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		var configuredTypes = new Godot.Collections.Array<Enums.InventoryType>();
		foreach (Enums.InventoryType type in inventoryTypes)
		{
			if (!configuredTypes.Contains(type)) configuredTypes.Add(type);
		}

		foreach (InventoryGrid inventory in InventoryGrids.Values)
		{
			foreach ((Item item, int _) in inventory.UniqueItems)
			{
				if (item == null) continue;
				InventoryOnItemRemoved(inventory, item);
				item.currentGrid = null;
				item.QueueFree();
			}
			inventory.ItemAdded -= InventoryOnItemAdded;
			inventory.ItemRemoved -= InventoryOnItemRemoved;
		}
		InventoryGrids.Clear();
		inventoryTypes.Clear();

		if (data.ContainsKey("inventory_types"))
		{
			var inventoryTypesArray = data["inventory_types"]
				.AsGodotArray<int>();
			foreach (int typeValue in inventoryTypesArray)
			{
				var inventoryType = (Enums.InventoryType)typeValue;
				if (inventoryType != Enums.InventoryType.None &&
				    !inventoryTypes.Contains(inventoryType))
					inventoryTypes.Add(inventoryType);
			}
		}

		if (data.ContainsKey("inventories"))
		{
			var savedInventories = data["inventories"]
				.AsGodotDictionary<string, Variant>();
			foreach (string typeName in savedInventories.Keys)
			{
				if (Enum.TryParse(typeName, out Enums.InventoryType savedType) &&
				    savedType != Enums.InventoryType.None &&
				    !inventoryTypes.Contains(savedType))
					inventoryTypes.Add(savedType);
			}
		}

		foreach (Enums.InventoryType configuredType in configuredTypes)
		{
			if (!inventoryTypes.Contains(configuredType))
				inventoryTypes.Add(configuredType);
		}

		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager != null)
		{
			foreach (Enums.InventoryType inventoryType in inventoryTypes)
			{
				InventoryGrid inventory = inventoryManager.GetInventoryGrid(inventoryType);
				if (inventory != null)
				{
					InventoryGrids.Add(inventoryType, inventory);
					inventory.ItemAdded += InventoryOnItemAdded;
					inventory.ItemRemoved += InventoryOnItemRemoved;
				}
			}
		}
		
		if (data.ContainsKey("inventories"))
		{
			var inventoriesData = (Godot.Collections.Dictionary<string, Variant>)data["inventories"];
			
			foreach (var inventoryEntry in inventoriesData)
			{
				var inventoryType = (Enums.InventoryType)Enum.Parse(typeof(Enums.InventoryType), inventoryEntry.Key);
				var inventoryData = (Godot.Collections.Dictionary<string, Variant>)inventoryEntry.Value;
				
				if (InventoryGrids.TryGetValue(
					    inventoryType,
					    out var inventoryGrid))
					inventoryGrid.LoadContents(inventoryData);
			}
		}
	}
}
