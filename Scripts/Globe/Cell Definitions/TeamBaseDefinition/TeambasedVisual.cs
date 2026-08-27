using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;


public partial class TeambasedVisual : CellDefinitionVisual
{
	public TeambasedVisual(HexCellDefinition parentCellDefinition, int cellIndex) : base(parentCellDefinition, cellIndex)
	{
	}
	
	public TeambasedVisual() : base()
	{
		parentCellDefinition = null;
		CellIndex = -1;
	}
	
	

	public override Dictionary<string, Callable> GetContextActions()
	{
		return base.GetContextActions();
	}
}
