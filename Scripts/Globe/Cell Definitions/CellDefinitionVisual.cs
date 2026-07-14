using Godot;
using System;
using System.Collections.Generic;

public abstract partial class CellDefinitionVisual : Node3D, IContextUser<CellDefinitionVisual>
{
	public int CellIndex;
	public HexCellDefinition parentCellDefinition;
	[Export] private CollisionObject3D collisionObject;
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

	public void BindDefinition(HexCellDefinition definition)
	{
		parentCellDefinition = definition;
		CellIndex = definition?.cellIndex ?? -1;
	}

	public void SetDefinitionVisible(bool visible)
	{
		Visible = visible;
		ProcessMode = visible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
		if (collisionObject != null)
			collisionObject.InputRayPickable = visible;
	}

	public override void _ExitTree()
	{
		parentCellDefinition?.ClearVisual(this);
		base._ExitTree();
	}

	public CellDefinitionVisual parent { get; set; }
}
