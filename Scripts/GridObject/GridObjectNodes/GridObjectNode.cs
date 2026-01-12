using Godot;
using System;

[GlobalClass]
public abstract partial class GridObjectNode : Node3D
{
	public GridObject parentGridObject { get; protected set; }

	public virtual void SetupCall(GridObject parentGridObject)
	{
		this.parentGridObject = parentGridObject;
		Setup();
	}
	
	protected abstract void Setup();

	public abstract Godot.Collections.Dictionary<string,Variant> Save();

	public abstract void Load(Godot.Collections.Dictionary<string,Variant> data);
}
