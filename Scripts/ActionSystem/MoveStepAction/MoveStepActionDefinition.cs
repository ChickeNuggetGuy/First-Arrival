using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class MoveStepActionDefinition : ActionDefinition
{
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		(Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data)
	{
		return new MoveStepAction(parent, startGridCell, targetGridCell, data);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell,  Dictionary<string, Variant> extraData,
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
		
		outdata.costs = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 },
		};
		var failedCosts = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, -1 },
			{ Enums.Stat.Stamina, -1 },
		};
		outdata.reason = "";


		if (!GetValidGridCells(parentGridObject,startingGridCell).Contains(targetGridCell))
		{
			outdata.reason = "Target grid cell is not valid";
			outdata.costs = failedCosts;
			return false;
		}
		
		if(!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell, out var neighbors))return false;
		if(neighbors.Count == 0) return false;
		if (!neighbors.Contains(startingGridCell))
		{
			outdata.reason = "Starting grid cell not neighbor of target grid cell";
			outdata.costs = failedCosts;
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
				outdata.reason = "No rotate action definition on grid object";
				outdata.costs = failedCosts;
				return false;
			}

			if (!rotateActionDefinition.CanTakeAction(parentGridObject, startingGridCell, targetGridCell,null,
				    out var rotateData
			    ))
			{
				outdata.reason = ($"Rotate couldn't execute for reason: {rotateData.reason} ");
				outdata.costs = failedCosts;
				return false;	
			}
			
			outdata.costs[Enums.Stat.TimeUnits] += rotateData.costs[Enums.Stat.TimeUnits];
			outdata.costs[Enums.Stat.Stamina] += rotateData.costs[Enums.Stat.Stamina];
		}

		if (startingGridCell.gridCoordinates.X - targetGridCell.gridCoordinates.X == 1 &&
		    startingGridCell.gridCoordinates.Z - targetGridCell.gridCoordinates.Z == 1)
		{
			//Cells Are diagonal to each other
			outdata.costs[Enums.Stat.TimeUnits] += 6;
			outdata.costs[Enums.Stat.Stamina] += 2;
		}
		else
		{
			outdata.costs[Enums.Stat.TimeUnits] += 4;
			outdata.costs[Enums.Stat.Stamina] += 2;
		}

		bool canAfford = parentGridObject.CanAffordStatCost(outdata.costs);
		if (!canAfford)
		{
			outdata.reason = "Can't afford stat costs"; ;
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
}