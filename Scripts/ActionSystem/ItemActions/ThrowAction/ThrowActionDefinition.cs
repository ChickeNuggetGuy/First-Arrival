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
public partial class ThrowActionDefinition : ItemActionDefinition
{
	public ThrowActionDefinition()
	{
		
	}
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		(System.Collections.Generic.Dictionary<Enums.Stat, int> costs, System.Collections.Generic.Dictionary<string, Variant> extraData) data)
	{
		return new ThrowAction(parent, startGridCell, targetGridCell, data);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, System.Collections.Generic.Dictionary<string, Variant> extraData,
		out (System.Collections.Generic.Dictionary<Enums.Stat, int> costs, System.Collections.Generic.Dictionary<string, Variant> extraData, string reason) outdata)
	{

		
		outdata = new();
		if (extraData != null && extraData.Count > 0)
		{
			outdata.extraData = extraData;
		}
		else
		{
			outdata =new (new System.Collections.Generic.Dictionary<Enums.Stat, int>(),
				new System.Collections.Generic.Dictionary<string, Variant>(), "");
		}
		
		var failedCosts = new System.Collections.Generic.Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, -1 },
			{ Enums.Stat.Stamina, -1 },
		};
		outdata.costs = new System.Collections.Generic.Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 }
		};
		Item item = extraData["item"].As<Item>();
		if (item == null)
		{
			outdata.reason = "Item not found";
			outdata.costs = failedCosts;
			return false;
		}
		
		
		var results  = Pathfinder.Instance.TryCalculateArcPath(startingGridCell, targetGridCell);
		bool success = (bool)results.Success;
		List<GridCell> path = (List<GridCell>)results.GridCellPath;
		
		if (path == null || path.Count == 0)
		{
			outdata.reason = "No path found";
			outdata.costs = failedCosts;
			return false;
		}

		RotateActionDefinition rotateActionDefinition =
			gridObject.ActionDefinitions
				.FirstOrDefault(a => a is RotateActionDefinition) as RotateActionDefinition;
		
		if (rotateActionDefinition == null)
		{
			outdata.reason = "Rotate action found";
			outdata.costs = failedCosts;
			return false;
		}
		
		GridCell currentGridCell = gridObject.GridPositionData.GridCell;
		Enums.Direction targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(currentGridCell, targetGridCell);

		if (gridObject.GridPositionData.Direction != targetDirection)
		{
			//Rotation Action needed!
			if (!rotateActionDefinition.CanTakeAction(gridObject, startingGridCell, targetGridCell, extraData,
				    out var rotateData))
			{
				outdata.reason =$" RotateAction failed because: {rotateData.reason}";
				outdata.costs = failedCosts;
				return false;
			}
			
			outdata.costs[Enums.Stat.TimeUnits] += rotateData.costs[Enums.Stat.TimeUnits];
			outdata.costs[Enums.Stat.Stamina] += rotateData.costs[Enums.Stat.Stamina];
			
		}

		outdata.costs[Enums.Stat.TimeUnits] += 2 * item.ItemData.weight;
		outdata.costs[Enums.Stat.Stamina] += 4 * 2 * item.ItemData.weight;
		
		if (!gridObject.CanAffordStatCost(outdata. costs))
		{
			outdata.reason = "Can't afford stat costs";
			outdata.costs =outdata.costs;
			return false;
		}

		Godot.Collections.Array<Vector3I> coordinatePath = new Godot.Collections.Array<Vector3I>();
		for (int i = 0; i < path.Count; i++)
		{
			coordinatePath.Add(path[i].gridCoordinates);
		}

		GD.Print(path.Count);
		outdata.reason = "Success!";
		outdata.extraData["path"] = coordinatePath;
		outdata.extraData["vectorPath"] = results.Vector3Path;
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		if (!GridSystem.Instance.TryGetCellsInRange(startingGridCell,new Vector2I(5,3), out List<GridCell> gridCells))
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
}
