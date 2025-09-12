using FirstArrival.Scripts.Inventory_System;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.ItemActions;

public interface IItemActionDefinition
{
	public Item Item { get; set; }
}