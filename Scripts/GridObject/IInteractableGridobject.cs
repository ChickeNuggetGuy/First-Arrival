using Godot;
using System;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

public interface IInteractableGridobject 
{
	public Dictionary<Enums.Stat, int> costs { get; protected set; }
	public void Interact();
}
