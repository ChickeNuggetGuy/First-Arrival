using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Array = Godot.Collections.Array;

[GlobalClass]
public partial class MoveActionDefinition : ActionDefinition
{
	public List<GridCell> path = new List<GridCell>();
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs)
	{
		return new MoveAction(parent, startGridCell, targetGridCell,this,  costs);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, out Dictionary<Enums.Stat, int> costs,
		out string reason)
	{
		var failedCosts = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, -1 },
			{ Enums.Stat.Stamina, -1 },
		};
		costs = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 }
		};
		
		if (!targetGridCell.state.HasFlag(Enums.GridCellState.Walkable))
		{
			reason = "Target grid cell is not walkable";
			costs = failedCosts;
			return false;
		}
		
		List<GridCell> tempPath  = Pathfinder.Instance.FindPath(startingGridCell, targetGridCell);
		if (tempPath == null || tempPath.Count == 0)
		{
			reason = "No path found";
			costs = failedCosts;
			return false;
		}

		MoveStepActionDefinition moveStepAction =
			gridObject.ActionDefinitions
				.FirstOrDefault(a => a is MoveStepActionDefinition) as MoveStepActionDefinition;
		
		if (moveStepAction == null)
		{
			reason = "No move step action found";
			costs = failedCosts;
			return false;
		}
		
		GridCell currentGridCell = gridObject.GridPositionData.GridCell;
		for (int i = 0; i < tempPath.Count; i++)
		{
			if (i +1 >= tempPath.Count) continue;
			GridCell nextGridCell = tempPath[i +1];

			if (!moveStepAction.CanTakeAction(gridObject, currentGridCell, nextGridCell,
				    out var moveStepCosts, out string moveStepReason))
			{
				reason =$" Move step action failed because: {moveStepReason}";
				costs = failedCosts;
				return false;
			}
			currentGridCell = nextGridCell;
			costs[Enums.Stat.TimeUnits] = moveStepCosts[Enums.Stat.TimeUnits];
			costs[Enums.Stat.Stamina] = moveStepCosts[Enums.Stat.Stamina];
		}

		if (!gridObject.CanAffordStatCost(costs))
		{
			reason = "Can't afford stat costs"; ;
			return false;
		}

		Godot.Collections.Array<Vector3I> vectorPath = new Godot.Collections.Array<Vector3I>();
		for (int i = 0; i < tempPath.Count; i++)
		{
			vectorPath.Add(tempPath[i].gridCoordinates);
		}
		reason = "Success!";
		path = tempPath;
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		return new List<GridCell>() { GridCell.Null };
	}

	public override bool GetIsUIAction() => true;
	
	public override string GetActionName() => "Move";
	
	public override MouseButton GetActionInput() => MouseButton.Left;
	
	public override bool GetIsAlwaysActive() => true;
}

