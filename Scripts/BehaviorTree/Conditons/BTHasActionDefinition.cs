using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree.Core;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class BTHasActionDefinition : BTCondition
{
	[Export] public ActionDefinition ActionDef { get; set; }
	[Export] public bool setSelectedAction = false;
	protected override bool Check()
	{
		if (ActionDef == null)
		{
			GD.Print("ActionDef is null");
			return false;
		}

		GridObject parentGridObject = Tree.ParentGridObject;
		List<ActionDefinition> actionDefinitions = new();
		
		if (parentGridObject == null)
		{
			GD.Print("ParentGridObject is null");
			return false;
		}

		if (!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out GridObjectActions actions))
		{
			GD.Print("GridObjectActions is null");
			return false;
		}
		
		actionDefinitions.AddRange(actions.ActionDefinitions);

		if (parentGridObject.TryGetGridObjectNode<GridObjectInventory>(out GridObjectInventory gridObjectInventory))
		{
			Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids = gridObjectInventory.InventoryGrids.Where(inv =>
				inv.Value.InventorySettings.HasFlag(Enums.InventorySettings.IsEquipmentinventory)).ToDictionary();
			
			foreach (KeyValuePair<Enums.InventoryType, InventoryGrid> inventory in inventoryGrids)
			{
				foreach (var itemKVP in inventory.Value.Items)
				{
					if (itemKVP.item == null) continue;
					actionDefinitions.AddRange(itemKVP.item.ItemData.ActionDefinitions);
				}
			}
		}

		ActionDefinition returnDefinition =
			actionDefinitions.FirstOrDefault(actionDefinition => actionDefinition.GetType() == ActionDef.GetType());
		
		if (returnDefinition != null)
		{
			GD.Print("Action Definition found: " );
			if(setSelectedAction)
				Blackboard.Set("selectedAction", ActionDef);
			return true;
		}
		else
		{
			GD.Print("Action Definition not found");
			return false;
		}
		
	}
}
