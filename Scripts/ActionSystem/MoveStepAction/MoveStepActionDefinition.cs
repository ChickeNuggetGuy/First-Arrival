using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class MoveStepActionDefinition : ActionDefinition
{
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs)
	{
		return new MoveStepAction(parent, startGridCell, targetGridCell,this, costs);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell,  out Dictionary<Enums.Stat, int> costs,
		out string reason)
	{
		costs = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 },
		};
		var failedCosts = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, -1 },
			{ Enums.Stat.Stamina, -1 },
		};
		reason = "";


		if (!GetValidGridCells(parentGridObject,startingGridCell).Contains(targetGridCell))
		{
			reason = "Target grid cell is not valid";
			costs = failedCosts;
			return false;
		}
		
		if(!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell, out var neighbors))return false;
		if(neighbors.Count == 0) return false;
		if (!neighbors.Contains(startingGridCell))
		{
			reason = "Starting grid cell not neighbor of target grid cell";
			costs = failedCosts;
			return false;
		}
		
		Enums.Direction targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(startingGridCell, targetGridCell);
		if (gridObject.GridPositionData.Direction != targetDirection)
		{
			//Not facing the correct Direction, Needs rotating
			RotateActionDefinition rotateActionDefinition =
				parentGridObject.ActionDefinitions.FirstOrDefault(a => a is RotateActionDefinition) as RotateActionDefinition;
			if (rotateActionDefinition == null)
			{
				reason = "No rotate action definition on grid object";
				costs = failedCosts;
				return false;
			}

			if (!rotateActionDefinition.CanTakeAction(parentGridObject, startingGridCell, targetGridCell,
				    out var rotateCosts, out string rotateReason))
			{
				if(rotateReason!= "Already facing in that direction")
				{
					reason= ($"Rotate couldn't execute for reason: {rotateReason} ");
					costs= failedCosts;
					return false;
				}
			}
			
			costs[Enums.Stat.TimeUnits] += rotateCosts[Enums.Stat.TimeUnits];
			costs[Enums.Stat.Stamina] += rotateCosts[Enums.Stat.Stamina];
		}

		if (startingGridCell.gridCoordinates.X - targetGridCell.gridCoordinates.X == 1 &&
		    startingGridCell.gridCoordinates.Z - targetGridCell.gridCoordinates.Z == 1)
		{
			//Cells Are diagonal to each other
			costs[Enums.Stat.TimeUnits] += 6;
			costs[Enums.Stat.Stamina] += 2;
		}
		else
		{
			costs[Enums.Stat.TimeUnits] += 4;
			costs[Enums.Stat.Stamina] += 2;
		}

		bool canAfford = parentGridObject.CanAffordStatCost(costs);
		if (!canAfford)
		{
			reason = "Can't afford stat costs"; ;
			return false;
		}

		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{ 
		GridSystem.Instance.TryGetGridCellNeighbors(startingGridCell, out var neighbors);
		return neighbors;	
		
	}
	public override string GetActionName() => "Move Step";
	public override bool GetIsUIAction() => false;
	public override MouseButton GetActionInput() => MouseButton.None;
	
	public override bool GetIsAlwaysActive() => false;
}