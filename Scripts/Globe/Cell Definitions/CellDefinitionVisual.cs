using Godot;
using System;
using System.Collections.Generic;

public abstract partial class CellDefinitionVisual : Node3D, IContextUser<CellDefinitionVisual>
{
	public int CellIndex;
	public HexCellDefinition parentCellDefinition;
	[Export] CollisionObject3D collisionObject;
	public CellDefinitionVisual(HexCellDefinition parentCellDefinition, int cellIndex)
	{
		this.parentCellDefinition = parentCellDefinition;
		this.CellIndex = cellIndex;
	}
	
	public CellDefinitionVisual()
	{
		this.parentCellDefinition = null;
		this.CellIndex = -1;
	}
	public abstract Dictionary<string, Callable> GetContextActions();

	public CellDefinitionVisual parent { get; set; }
}
