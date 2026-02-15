using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree.Core;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class IsItemEquipped : BTCondition
{
	[Export] public Godot.Collections.Array<ItemActionDefinition> ItemActionDefinitions { get; protected set; }
	protected override bool Check()
	{

		GridObject parentGridObject = Tree.ParentGridObject;
		if (parentGridObject == null) return false;

		if (!parentGridObject.TryGetGridObjectNode<GridObjectInventory>(out GridObjectInventory gridObjectInventory))
		{
			return false;
		}

		Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = gridObjectInventory.InventoryGrids.Where(inv =>
			inv.Value.InventorySettings.HasFlag(Enums.InventorySettings.IsEquipmentinventory)).ToDictionary();


		foreach (KeyValuePair<Enums.InventoryType, InventoryGrid> inventory in inventoryGrids)
		{
			foreach (var item in inventory.Value.Items)
			{
				bool success = true;
				foreach (ItemActionDefinition itemActionDefinition in ItemActionDefinitions)
				{
					if (item.item.ItemData.ActionDefinitions.All(a => a.GetType() != itemActionDefinition.GetType()))
					{
						success = false;
					}
					
					if (success)
					{
						Blackboard.Set("item", item.item.ItemData);
						return true;
					}
				}
			}
		}
		
		return false;
	}
}
