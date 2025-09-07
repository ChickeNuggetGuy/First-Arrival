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

	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		(Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data)
	{
		return new MoveAction(parent, startGridCell, targetGridCell, data);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, Dictionary<string, Variant> extraData,
		out(Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData, string reason) outdata)
	{
		outdata = new();
		if (extraData != null && extraData.Count > 0)
		{
			outdata.extraData = extraData;
		}
		else
		{
			outdata =new (new Dictionary<Enums.Stat, int>(),
				new Dictionary<string, Variant>(), "");
		}
		
		var failedCosts = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, -1 },
			{ Enums.Stat.Stamina, -1 },
		};
		outdata.costs = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 }
		};
		outdata.extraData = new Dictionary<string, Variant>();
		
		if (!targetGridCell.state.HasFlag(Enums.GridCellState.Walkable))
		{
			outdata.reason = "Target grid cell is not walkable";
			outdata.costs = failedCosts;
			return false;
		}
		
		List<GridCell> path  = Pathfinder.Instance.FindPath(startingGridCell, targetGridCell);
		if (path == null || path.Count == 0)
		{
			outdata.reason = "No path found";
			outdata.costs = failedCosts;
			return false;
		}

		MoveStepActionDefinition moveStepAction =
			gridObject.ActionDefinitions
				.FirstOrDefault(a => a is MoveStepActionDefinition) as MoveStepActionDefinition;
		
		if (moveStepAction == null)
		{
			outdata.reason = "No move step action found";
			outdata.costs = failedCosts;
			return false;
		}
		
		GridCell currentGridCell = gridObject.GridPositionData.GridCell;
		for (int i = 0; i < path.Count; i++)
		{
			if (i +1 >= path.Count) continue;
			GridCell nextGridCell = path[i +1];

			if (!moveStepAction.CanTakeAction(gridObject, currentGridCell, nextGridCell, null,
				    out var moveStepData))
			{
				outdata.reason =$" Move step action failed because: {moveStepData.reason}";
				outdata.costs = failedCosts;
				return false;
			}
			currentGridCell = nextGridCell;
			outdata.costs[Enums.Stat.TimeUnits] = moveStepData.costs[Enums.Stat.TimeUnits];
			outdata.costs[Enums.Stat.Stamina] = moveStepData.costs[Enums.Stat.Stamina];
		}

		if (!gridObject.CanAffordStatCost(outdata. costs))
		{
			outdata.reason = "Can't afford stat costs";
			outdata.costs =outdata.costs;
			return false;
		}

		Godot.Collections.Array<Vector3I> vectorPath = new Godot.Collections.Array<Vector3I>();
		for (int i = 0; i < path.Count; i++)
		{
			vectorPath.Add(path[i].gridCoordinates);
		}
		outdata.reason = "Success!";
		outdata.extraData["path"] = vectorPath;
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		return new List<GridCell>() { GridCell.Null };
	}

	public override bool GetIsUIAction() => true;
	
	public override string GetActionName() => "Move";
}

