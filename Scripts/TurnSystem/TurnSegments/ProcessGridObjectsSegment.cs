

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
		List<GridObject> gridObjects = teamHolder.GridObjects.Values.SelectMany(units => units)
			.Where(unit => unit.IsActive || unit.Condition?.State == Enums.UnitCondition.Unconscious)
			.Distinct().ToList();
		if (gridObjects.Count == 0) return;
		
		// Bleeding can kill and remove a unit while this segment is processing.
		// Iterate over a snapshot so the team list may safely change.
		foreach (var gridObject in gridObjects.ToArray())
		{
			if (gridObject.CanAct && gridObject.TryGetGridObjectNode<GridObjectSight>(out var gridObjectSight))
				gridObjectSight.CalculateSightArea();
			
			if(!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) continue;

			GridObjectStat health = null;
			statHolder.TryGetStat(Enums.Stat.Health, out health);
			if (gridObject.Condition != null)
			{
				bool wasUnconscious = gridObject.Condition.State == Enums.UnitCondition.Unconscious;
				gridObject.Condition.ProcessTurn();
				if (!gridObject.CanAct || wasUnconscious) continue;
			}
			else health?.ApplyFatalWoundBleeding();
			if (!gridObject.CanAct) continue;

			GridObjectStat[]stats = statHolder.Stats.Where(stat => stat.Stat != Enums.Stat.Stun &&
				stat.turnBehavior != Enums.StatTurnBehavior.None).ToArray();

			foreach (GridObjectStat stat in stats)
			{
				if (stat.Stat == Enums.Stat.Stamina && health != null)
				{
					float woundPenalty =
						health.GetStaminaRecoveryPenalty(stat.CurrentValue);
					stat.OnTurnEnded(incrementPenalty: woundPenalty);
				}
				else
				{
					stat.OnTurnEnded();
				}
			}

			if (
				health != null
				&& statHolder.TryGetStat(Enums.Stat.TimeUnits, out GridObjectStat timeUnits)
			)
			{
				timeUnits.SetValue(timeUnits.CurrentValue * health.GetTimeUnitMultiplier());
			}
		}
		GD.Print("Execute ProcessGridObjectsSegment Done");
	}
}
