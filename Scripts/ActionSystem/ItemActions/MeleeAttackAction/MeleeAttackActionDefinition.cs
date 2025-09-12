using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MeleeAttackActionDefinition : ActionDefinition, IItemActionDefinition
{
	public Item Item { get; set; }
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs)
	{
		return new MeleeAttackAction(parent, startGridCell, targetGridCell, this, costs);
	}

	public override bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, out Dictionary<Enums.Stat, int> costs,
		out string reason)
	{
		costs = new Dictionary<Enums.Stat, int>()
		{
			{ Enums.Stat.TimeUnits, 0 },
			{ Enums.Stat.Stamina, 0 }
		};
		reason = "N/A";

		Item item = Item;

		
		if (gridObject == null)
		{
			costs[Enums.Stat.TimeUnits] = -1;
			costs[Enums.Stat.Stamina] = -1;
			reason = "Gridobject is null";
			return false;
		}

		if (startingGridCell == null || targetGridCell == null)
		{
			costs[Enums.Stat.TimeUnits] = -1;
			costs[Enums.Stat.Stamina] = -1;
			reason = "Starting or target gridcell is null";
			return false;
		}

		if (!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell, out var neighbors))
		{
			costs[Enums.Stat.TimeUnits] = -1;
			costs[Enums.Stat.Stamina] = -1;
			reason = "Could not find neighbors for target gridcell";
			return false;
		}

		if (neighbors.Contains(startingGridCell))
		{
			//Unit Already adjacent, no need to move unit
			Enums.Direction targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(startingGridCell, targetGridCell);
			if (targetDirection != gridObject.GridPositionData.Direction)
			{
				//Gridobject still needs rotation to face the correct direction
				RotateActionDefinition rotateAction = gridObject.ActionDefinitions.FirstOrDefault(a => a is RotateActionDefinition) as RotateActionDefinition;

				if (rotateAction == null)
				{
					costs[Enums.Stat.TimeUnits] = -1;
					costs[Enums.Stat.Stamina] = -1;
					reason = "unit cannot rotate when it is needed";
					return false;
				}

				if (!rotateAction.CanTakeAction(gridObject, startingGridCell, targetGridCell,
					    out var rotateCosts, out string rotateReason))
				{
					costs[Enums.Stat.TimeUnits] = -1;
					costs[Enums.Stat.Stamina] = -1;
					reason = $"Rotate Action CanTakeAction failed: {rotateReason}";
					return false;
				}

				foreach (var statCost in rotateCosts)
				{
					costs[statCost.Key] += statCost.Value;
				}
			}
		}
		else
		{
			GridCell target = neighbors.FirstOrDefault(neighbor =>
			{
				if (neighbor.state.HasFlag(Enums.GridCellState.Walkable)) return true;
				else return false;
			});
			
			if (target == null)
			{
				GD.Print("Target gridcell is null");
			}
			List<GridCell> path = Pathfinder.Instance.FindPath(startingGridCell, target, false);
			
			//Gridobject needs movement to be adjacent
			MoveActionDefinition moveAction = gridObject.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition) as MoveActionDefinition;

			if (moveAction == null)
			{
				costs[Enums.Stat.TimeUnits] = -1;
				costs[Enums.Stat.Stamina] = -1;
				reason = "unit cannot move when it is needed";
				return false;
			}

			if (!moveAction.CanTakeAction(gridObject, startingGridCell, target,
				    out var  moveCosts, out string moveReason))
			{
				costs[Enums.Stat.TimeUnits] = -1;
				costs[Enums.Stat.Stamina] = -1;
				reason = $"move Action CanTakeAction failed: {moveReason}";
				return false;
			}

			foreach (var statCost in moveCosts)
			{
				costs[statCost.Key] += statCost.Value;
			}
		}
		
		//TODO: Have custom costs per Item
		costs[Enums.Stat.TimeUnits] = 6 * item.ItemData.weight;
		costs[Enums.Stat.TimeUnits] = 8 * item.ItemData.weight;
		reason = "success";


		if (!gridObject.CanAffordStatCost(costs))
		{
			reason = $"unit cannot afford action costs";
			return false;
		}
		
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		if (!GridSystem.Instance.TryGetGridCellsInRange(startingGridCell, new Vector2I(8, 2),
			    out List<GridCell> neighbors))
		{
			return null;
		}
		return neighbors.Where(n =>
		{
			return n.HasGridObject();
		}).ToList();
	}

	public override bool GetIsUIAction() => true;

	public override string GetActionName() => "Melee";

	public override MouseButton GetActionInput() => MouseButton.Left;

	public override bool GetIsAlwaysActive() => false;

}
