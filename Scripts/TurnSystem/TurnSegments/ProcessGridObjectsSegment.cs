

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class ProcessGridObjectsSegment: TurnSegment
{
	GridObjectTeamHolder teamHolder;
	protected override async Task _Setup()
	{
		GD.Print("Finding Team Holder");
		teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(parentTurn.team);
		return;
	}

	protected override async Task _Execute()
	{
		GD.Print("Execute ProcessGridObjectsSegment");
		List<GridObject> gridObjects = teamHolder.GridObjects[Enums.GridObjectState.Active];
		if (gridObjects.Count == 0) return;
		
		foreach (var gridObject in teamHolder.GridObjects[Enums.GridObjectState.Active])
		{
			if(!gridObject.TryGetGridObjectNode<GridObjectSight>( out GridObjectSight gridObjectSight )) continue;
			
			gridObjectSight.CalculateSightArea();
			
			if(!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) continue;


			
			GridObjectStat[]stats = statHolder.Stats.Where(stat => stat.turnBehavior != Enums.StatTurnBehavior.None).ToArray();

			foreach (GridObjectStat stat in stats)
			{
				stat.OnTurnEnded();
			}
		}
		GD.Print("Execute ProcessGridObjectsSegment Done");
	}
}