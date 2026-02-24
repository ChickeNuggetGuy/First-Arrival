using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.AIActions;

[GlobalClass]
public partial class Rotate360ActionDefinition : ActionDefinition
{
	private int exectutionCount;
	public override Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell, 
		Godot.Collections.Dictionary<Enums.Stat, int> costs)
	{
		return new Rotate360Action(parent, startGridCell, targetGridCell, this, costs);
	}

	protected override bool OnValidateAndBuildCosts(GridObject gridObject, GridCell startingGridCell,
		GridCell targetGridCell, Godot.Collections.Dictionary<Enums.Stat, int> costs, out string reason)
	{

		if (!gridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode))
		{
			reason = "No grid object Action node found";
			return false;
		}
		
		if (!gridObjectActionsNode.ActionDefinitions.Any(ad => ad is RotateActionDefinition))
		{
			reason = "RotateActionDefinition not found on GridObject";
			return false;
		}
		
		// 8 steps for a 360 rotation (45 degrees each)
		const int rotationSteps = 8;
		
		// Assuming cost of 1 TU and 1 Stamina per 45-degree rotation step
		AddCost(costs, Enums.Stat.TimeUnits, rotationSteps * 1);
		AddCost(costs, Enums.Stat.Stamina, rotationSteps * 1);
		
		reason = "Success!";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		// This action is performed on the spot.
		return new List<GridCell> { startingGridCell };
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		if (!parentGridObject.TryGetGridObjectNode<GridObjectSight>(out var gridObjectSight))
		{
			return (targetGridCell, 0);
		}

		if (gridObjectSight.SeenGridObjects.Count > 1 || gridObjectSight.PreviouslySeenGridObjects.Count > 1)
		{
			return (targetGridCell, 0);
		}

		return (targetGridCell, GD.RandRange(50,100));
	}

	public override bool GetIsUIAction() => false;

	public override string GetActionName() => "Rotate 360";

	public override MouseButton GetActionInput() => MouseButton.Right;

	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => false;
}