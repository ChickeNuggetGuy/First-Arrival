using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

public interface IInteractableGridobject 
{
	public Godot.Collections.Dictionary<Enums.Stat, int> costs { get; protected set; }
	public void Interact();

	public List<GridCell> GetInteractableCells();
}
