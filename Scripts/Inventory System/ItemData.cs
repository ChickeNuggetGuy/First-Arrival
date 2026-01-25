using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Inventory_System;

[Tool]
[GlobalClass]
public partial class ItemData : Resource
{
	[Export]
	public int ItemID { get; protected set; }

	[Export]
	public string ItemName { get; protected set; }

	[Export(PropertyHint.MultilineText)]
	public string ItemDescription { get; protected set; }

	[Export]
	public Texture2D ItemIcon { get; protected set; }

	[Export(PropertyHint.ResourceType, "GridShape")]
	public GridShape ItemShape { get; set; }

	[Export] public int weight;
	
	[Export]public PackedScene ItemScene { get; protected set; }

	[Export(PropertyHint.ResourceType, "ItemActionDefinition")]
	public Array<ActionDefinition> ActionDefinitions;
	
	[Export] protected Dictionary<string, Variant> extraData = new();
	[Export] public int MaxStackSize { get; protected set; } = 1;
	[Export] public Enums.ItemSettings ItemSettings { get; protected set; }
	public static Item CreateItem(ItemData itemData)
	{
		Item retVal = new Item();
		
		
		retVal.Init((ItemData)itemData.Duplicate());
		return retVal;
	}


	public bool TryGetData(string key, out Variant variant) => extraData.TryGetValue(key, out variant);
	
}