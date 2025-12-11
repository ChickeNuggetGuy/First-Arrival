using Godot;
using System;

[GlobalClass]
public abstract partial class GridObjectNode : Node
{
	public GridObject parentGridObject { get; protected set; }

	public virtual void SetupCall(GridObject parentGridObject)
	{
		this.parentGridObject = parentGridObject;
		Setup();
	}
	
	protected abstract void Setup();
}
