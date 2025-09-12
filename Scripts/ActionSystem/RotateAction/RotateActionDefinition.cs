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
		GridCell targetGridCell, Dictionary<Enums.Stat, int> costs)
	{
		return new RotateAction(parent, startGridCell, targetGridCell,this,costs,
			RotationHelperFunctions.GetDirectionBetweenCells(startGridCell, targetGridCell));
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, out Dictionary<Enums.Stat, int> costs,
		out string reason)
	{
		bool success = false;
		costs =  new Dictionary<Enums.Stat, int>();
		var failCosts = new Dictionary<Enums.Stat, int>()
		{
			{Enums.Stat.TimeUnits, -1},
			{Enums.Stat.Stamina, -1}
		};
		reason = "";
    
		Enums.Direction targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(startingGridCell, targetGridCell );

		if (gridObject.GridPositionData.Direction == targetDirection)
		{
			 costs = failCosts;
			 reason  = "Already facing in that direction";
			return false;
		}
		int rotationSteps = RotationHelperFunctions.GetRotationStepsBetweenDirections(gridObject.GridPositionData.Direction, targetDirection);
		
		 costs[Enums.Stat.TimeUnits] = Mathf.Abs( rotationSteps) * 1;
		 costs[Enums.Stat.Stamina] = Mathf.Abs(  rotationSteps) * 1;
		
		if (!gridObject.TryGetStat(Enums.Stat.TimeUnits, out GridObjectStat timeUnitStat))
		{ 
			 costs = failCosts;
			 reason = "GridObject does not have time unit stat";
			return false;
		}
		
		if (timeUnitStat.CurrentValue <  costs[Enums.Stat.TimeUnits])
		{
			//Not enough Time units for action
			 costs = failCosts;
			 reason = "GridObject does not have enough time units Stat";
			return false;
		}
		
		if (!gridObject.TryGetStat(Enums.Stat.Stamina, out GridObjectStat staminaStat))
		{
			//Gridobject doesn not Stamina Stat
			 costs = failCosts;
			 reason = "GridObject does not have staminaStat stat";
			return false;
		}

		if (staminaStat.CurrentValue <  costs[Enums.Stat.Stamina])
		{
			 costs = failCosts;
			 reason = "GridObject does not have enough stamina Stat";
			return false;
		}
		
		 reason = targetDirection.ToString();
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		return new  List<GridCell>() { GridCell.Null};
	}
	
	public override string GetActionName() => "Rotate";
	public override bool GetIsUIAction() => true;

	public override MouseButton GetActionInput() => MouseButton.Right;
	
	public override bool GetIsAlwaysActive() => true;
}
