using Godot;
using System;
using System.Collections.Generic;

public partial class MissionCellVisual : CellDefinitionVisual
{
	public MissionCellVisual() : base()
	{
	}
	// Called when the node enters the scene tree for the first time.
	public MissionCellVisual(HexCellDefinition parentCellDefinition, int cellIndex) : base(parentCellDefinition, cellIndex)
	{
	}

	public override Dictionary<string, Callable> GetContextActions()
	{
		throw new NotImplementedException();
	}
}
