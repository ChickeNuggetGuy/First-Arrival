using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class RotateActionDefinition : ActionDefinition
{

	public RotateActionDefinition()
	{
		
	}
	public override Action InstantiateAction(GridObject parent, 
		GridCell startGridCell, 
		GridCell targetGridCell, (Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data)
	{
		return new RotateAction(parent, startGridCell, targetGridCell, data,
			RotationHelperFunctions.GetDirectionBetweenCells(startGridCell, targetGridCell));
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, Dictionary<string, Variant> extraData,
		out (Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData, string reason) outdata)
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
		
		bool success = false;
		outdata = new ValueTuple<Dictionary<Enums.Stat, int>, Dictionary<string, Variant>, string>();
		outdata. costs =  new Dictionary<Enums.Stat, int>();
		var failCosts = new Dictionary<Enums.Stat, int>()
		{
			{Enums.Stat.TimeUnits, -1},
			{Enums.Stat.Stamina, -1}
		};
		outdata.reason = "";
    
		Enums.Direction targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(startingGridCell, targetGridCell );

		if (gridObject.GridPositionData.Direction == targetDirection)
		{
			success = false;
			outdata.costs = failCosts;
			outdata.reason  = "Already facing in that direction";
			return success;
		}
		int rotationSteps = RotationHelperFunctions.GetRotationStepsBetweenDirections(gridObject.GridPositionData.Direction, targetDirection);
		
		outdata.costs[Enums.Stat.TimeUnits] = Mathf.Abs( rotationSteps) * 1;
		outdata.costs[Enums.Stat.Stamina] = Mathf.Abs(  rotationSteps) * 1;
		
		if (!gridObject.TryGetStat(Enums.Stat.TimeUnits, out GridObjectStat timeUnitStat))
		{ 
			success = false;
			outdata.costs = failCosts;
			outdata.reason = "GridObject does not have time unit stat";
			return success;
		}
		
		if (timeUnitStat.CurrentValue < outdata.costs[Enums.Stat.TimeUnits])
		{
			//Not enough Time units for action
			success = false;
			outdata.costs = failCosts;
			outdata.reason = "GridObject does not have enough time units Stat";
			return success;
		}
		
		if (!gridObject.TryGetStat(Enums.Stat.Stamina, out GridObjectStat staminaStat))
		{
			//Gridobject doesn not Stamina Stat
			success = false;
			outdata.costs = failCosts;
			outdata.reason = "GridObject does not have staminaStat stat";
			return success;
		}

		if (staminaStat.CurrentValue < outdata.costs[Enums.Stat.Stamina])
		{
			success = false;
			outdata.costs = failCosts;
			outdata.reason = "GridObject does not have enough stamina Stat";
			return success;
		}
		
		
		success = true;
		
		outdata.reason = targetDirection.ToString();
		return success;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		return new  List<GridCell>() { GridCell.Null};
	}
	
	public override string GetActionName() => "Rotate";
	public override bool GetIsUIAction() => true;
}
