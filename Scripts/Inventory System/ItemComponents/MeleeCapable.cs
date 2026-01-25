using Godot;

namespace FirstArrival.Scripts.Inventory_System;

[GlobalClass]
public partial class MeleeCapable : ItemComponent
{
	public int Damage { get; set; }
}