using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Inventory_System;

[GlobalClass]
public partial class ItemData : Resource
{
	[Export]
	public string ItemID { get; protected set; }

	[Export]
	public string ItemName { get; protected set; }

	[Export(PropertyHint.MultilineText)]
	public string ItemDescription { get; protected set; }

	[Export]
	public Texture2D ItemIcon { get; protected set; }

	[Export(PropertyHint.ResourceType, "GridShape")]
	public GridShape ItemShape { get; set; }
	
	[Export]public PackedScene ItemScene { get; protected set; }

	[Export(PropertyHint.ResourceType, "ActionDefinition")]
	public ActionDefinition[] ActionDefinitions;

	[Export] public int weight { get; protected set; } = 2;

	[Export] public int MaxStackSize { get; protected set; } = 1;
	[Export] public Enums.ItemSettings ItemSettings { get; protected set; }
	[Export]public int Damage { get; set; }
	[Export] public int Range { get; protected set; } = 1;
	public static Item CreateItem(ItemData itemData)
	{
		Item retVal = new Item();
		
		
		retVal.Init((ItemData)itemData.Duplicate());
		return retVal;
	}


}