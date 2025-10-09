

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
		teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(parentTurn.team);
		return;
	}

	protected override async Task _Execute()
	{
		List<GridObject> gridObjects = teamHolder.GridObjects[Enums.GridObjectState.Active];
		if (gridObjects.Count == 0) return;
		
		foreach (var gridObject in teamHolder.GridObjects[Enums.GridObjectState.Active])
		{
			foreach (var sight in gridObject.Sights)
			{
				sight.CalculateSightArea();
			}
			
			GridObjectStat[]stats = gridObject.Stats.Where(stat => stat.turnBehavior != Enums.StatTurnBehavior.None).ToArray();

			foreach (GridObjectStat stat in stats)
			{
				stat.OnTurnEnded();
			}
		}
	}
}