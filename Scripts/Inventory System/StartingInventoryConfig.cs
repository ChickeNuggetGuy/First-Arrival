using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class StartingInventoryConfig : Resource
{
	[Export] public Enums.InventoryType InventoryType;
	[Export] public Godot.Collections.Array<ItemData> Items = new();
}