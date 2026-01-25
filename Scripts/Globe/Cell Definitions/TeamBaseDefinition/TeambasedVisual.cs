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
		Dictionary<string, Callable> retVal = new Dictionary<string, Callable>();

		TeamBaseCellDefinition baseDef = parentCellDefinition as TeamBaseCellDefinition;
		
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		GlobeTeamHolder playerHolder = teamManager.GetTeamData(Enums.UnitTeam.Player);
		
		
		if (baseDef == null)
		{
			GD.PrintErr("Team base def is null!");
			return retVal;
		}

		foreach (Craft craft in baseDef.CraftList)
		{
			if(craft == null) continue;
			
			retVal.Add($"Send {craft.ItemName}", Callable.From(() => teamManager.SetSendCraftMode(true,playerHolder, craft)));
		}
		
		return retVal;
	}
}
