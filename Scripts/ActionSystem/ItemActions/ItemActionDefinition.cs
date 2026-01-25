using Godot;
using System;
using FirstArrival.Scripts.Inventory_System;

[GlobalClass]
public abstract partial class ItemActionDefinition : ActionDefinition
{
	public Item Item { get; set; }
}
