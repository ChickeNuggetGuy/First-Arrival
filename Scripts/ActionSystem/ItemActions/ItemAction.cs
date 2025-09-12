using System.Collections.Generic;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.ItemActions;

interface IItemAction
{
	public Item Item { get; set; }
}