using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MoveStepActionDefinition : ActionDefinition
{
  public override Action InstantiateAction(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs
  )
  {
    return new MoveStepAction(parent, startGridCell, targetGridCell, this, costs);
  }

  protected override bool OnValidateAndBuildCosts(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    // Validate adjacency and walkability
    var valid = GetValidGridCells(gridObject, startingGridCell);
    if (valid == null || !valid.Contains(targetGridCell))
    {
      reason = "Target grid cell is not valid";
      return false;
    }

    if (
      !GridSystem.Instance.TryGetGridCellNeighbors(startingGridCell, out var neighbors)
      || neighbors == null
      || !neighbors.Contains(targetGridCell)
    )
    {
      reason = "Target is not a neighbor of starting grid cell";
      return false;
    }

    // Determine rotation cost based on actual transform-facing
    var currentFacing = RotationHelperFunctions.GetDirectionFromRotation3D(
      gridObject.Rotation.Y
    );
    var targetFacing = RotationHelperFunctions.GetDirectionBetweenCells(
      startingGridCell,
      targetGridCell
    );

    if (currentFacing != targetFacing)
    {
      int rotationSteps = RotationHelperFunctions
        .GetRotationStepsBetweenDirections(currentFacing, targetFacing);
      AddCost(costs, Enums.Stat.TimeUnits, Mathf.Abs(rotationSteps) * 1);
      AddCost(costs, Enums.Stat.Stamina, Mathf.Abs(rotationSteps) * 1);
    }

    bool diagonal =
      Mathf.Abs(
        startingGridCell.gridCoordinates.X - targetGridCell.gridCoordinates.X
      ) == 1
      && Mathf.Abs(
        startingGridCell.gridCoordinates.Z - targetGridCell.gridCoordinates.Z
      ) == 1;

    if (diagonal)
    {
      AddCost(costs, Enums.Stat.TimeUnits, 6);
      AddCost(costs, Enums.Stat.Stamina, 2);
    }
    else
    {
      AddCost(costs, Enums.Stat.TimeUnits, 4);
      AddCost(costs, Enums.Stat.Stamina, 2);
    }

    reason = "Success!";
    return true;
  }

  protected override List<GridCell> GetValidGridCells(
    GridObject gridObject,
    GridCell startingGridCell
  )
  {
    GridSystem.Instance.TryGetGridCellNeighbors(startingGridCell, out var neighbors);
    return neighbors;
  }

  public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
  {
	  return (targetGridCell, 0);
  }
  
  public override string GetActionName() => "Move Step";
  public override bool GetIsUIAction() => false;
  public override MouseButton GetActionInput() => MouseButton.None;
  public override bool GetIsAlwaysActive() => false;
  
  public override bool GetRemainSelected() => false;
}