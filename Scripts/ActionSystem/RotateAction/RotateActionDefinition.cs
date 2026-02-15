using Godot;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class RotateActionDefinition : ActionDefinition
{
	public RotateActionDefinition() { }

	public override Action InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs
	)
	{
		return new RotateAction(
			parent,
			startGridCell,
			targetGridCell,
			this,
			costs,
			RotationHelperFunctions.GetDirectionBetweenCells(startGridCell, targetGridCell)
		);
	}

	protected override bool OnValidateAndBuildCosts(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs,
		out string reason
	)
	{
		var targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(
			startingGridCell,
			targetGridCell
		);

		// Determine current facing from the actual transform (Rotation.Y)
		var currentFacing = RotationHelperFunctions.GetDirectionFromRotation3D(
			gridObject.visualMesh.Rotation.Y
		);

		if (currentFacing == targetDirection)
		{
			reason = "Already facing in that direction";
			return false;
		}

		int rotationSteps = RotationHelperFunctions.GetRotationStepsBetweenDirections(
			currentFacing,
			targetDirection
		);

		AddCost(costs, Enums.Stat.TimeUnits, Mathf.Abs(rotationSteps) * 1);
		AddCost(costs, Enums.Stat.Stamina, Mathf.Abs(rotationSteps) * 1);

		reason = targetDirection.ToString();
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		return new List<GridCell> { GridCell.Null };
	}

	public override string GetActionName() => "Rotate";
	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		return (targetGridCell, 0);
	}

	public override bool GetIsUIAction() => true;
	public override MouseButton GetActionInput() => MouseButton.Right;
	public override bool GetIsAlwaysActive() => true;
	
	public override bool GetRemainSelected() => true;
}