using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.ActionSystem.ItemActions.ThrowAction;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;
using Array = Godot.Collections.Array;

[GlobalClass]
public partial class ThrowActionDefinition : ActionDefinition, IItemActionDefinition
{
	public Item Item { get; set; }
	public List<GridCell> path = new List<GridCell>();
	public Vector3[] vectorPath;
	public ThrowActionDefinition()
	{
		
	}
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell, System.Collections.Generic.Dictionary<Enums.Stat, int> costs)
	{
		return new ThrowAction(parent, startGridCell, targetGridCell,this,vectorPath, costs);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, out System.Collections.Generic.Dictionary<Enums.Stat, int> costs,
		out string reason)
	{
		var failedCosts = new System.Collections.Generic.Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, -1 },
			{ Enums.Stat.Stamina, -1 },
		};
		
		costs = new System.Collections.Generic.Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 }
		};
		
		if (Item == null)
		{
			reason = "Item not found";
			costs = failedCosts;
			return false;
		}
		
		
		var results  = Pathfinder.Instance.TryCalculateArcPath(startingGridCell, targetGridCell);
		bool success = (bool)results.Success;
		List<GridCell> path = (List<GridCell>)results.GridCellPath;
		
		if (path == null || path.Count == 0)
		{
			reason = "No path found";
			costs = failedCosts;
			return false;
		}

		RotateActionDefinition rotateActionDefinition =
			gridObject.ActionDefinitions
				.FirstOrDefault(a => a is RotateActionDefinition) as RotateActionDefinition;
		
		if (rotateActionDefinition == null)
		{
			reason = "Rotate action found";
			costs = failedCosts;
			return false;
		}
		
		GridCell currentGridCell = gridObject.GridPositionData.GridCell;
		Enums.Direction targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(currentGridCell, targetGridCell);

		if (gridObject.GridPositionData.Direction != targetDirection)
		{
			//Rotation Action needed!
			if (!rotateActionDefinition.CanTakeAction(gridObject, startingGridCell, targetGridCell,
				    out var rotateCosts,out string rotateReason))
			{
				reason =$" RotateAction failed because: {rotateReason}";
				costs = failedCosts;
				return false;
			}
			
			costs[Enums.Stat.TimeUnits] += rotateCosts[Enums.Stat.TimeUnits];
			costs[Enums.Stat.Stamina] += rotateCosts[Enums.Stat.Stamina];
		}

		costs[Enums.Stat.TimeUnits] += 2 * Item.ItemData.weight;
		costs[Enums.Stat.Stamina] += 4 * 2 * Item.ItemData.weight;
		
		if (!gridObject.CanAffordStatCost(costs))
		{
			reason = "Can't afford stat costs";
			return false;
		}

		List<Vector3> tempPath  = new List<Vector3>();
		for (int i = 0; i < path.Count; i++)
		{
			tempPath.Add(path[i].gridCoordinates);
		}
		vectorPath = tempPath.ToArray();

		GD.Print(path.Count);
		reason = "Success!";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		if (!GridSystem.Instance.TryGetGridCellsInRange(startingGridCell,new Vector2I(5,3), out List<GridCell> gridCells))
		{
			return null;
		}
		else
		{
			return gridCells;
		}
	}

	public override bool GetIsUIAction() => true;
	public override string GetActionName() => "Throw";
	
	public override MouseButton GetActionInput() => MouseButton.Left;
	
	public override bool GetIsAlwaysActive() => false;

}
