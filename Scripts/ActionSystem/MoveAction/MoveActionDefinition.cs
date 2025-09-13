using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MoveActionDefinition : ActionDefinition
{
  public List<GridCell> path = new List<GridCell>();

  public override Action InstantiateAction(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs
  )
  {
    return new MoveAction(parent, startGridCell, targetGridCell, this, costs);
  }

  protected override bool OnValidateAndBuildCosts(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    if (!targetGridCell.state.HasFlag(Enums.GridCellState.Walkable))
    {
      reason = "Target grid cell is not walkable";
      return false;
    }

    List<GridCell> tempPath =
      Pathfinder.Instance.FindPath(startingGridCell, targetGridCell);

    if (tempPath == null || tempPath.Count == 0)
    {
      reason = "No path found";
      return false;
    }

    // Simulate costs along the path with evolving facing, based on transform.
    var facing = RotationHelperFunctions.GetDirectionFromRotation3D(
      gridObject.Rotation.Y
    );

    for (int i = 0; i < tempPath.Count - 1; i++)
    {
      GridCell current = tempPath[i];
      GridCell next = tempPath[i + 1];

      var stepDir = RotationHelperFunctions.GetDirectionBetweenCells(
        current,
        next
      );

      if (facing != stepDir)
      {
        int steps = RotationHelperFunctions.GetRotationStepsBetweenDirections(
          facing,
          stepDir
        );
        AddCost(costs, Enums.Stat.TimeUnits, Mathf.Abs(steps) * 1);
        AddCost(costs, Enums.Stat.Stamina, Mathf.Abs(steps) * 1);
        facing = stepDir;
      }

      bool diagonal =
        Mathf.Abs(current.gridCoordinates.X - next.gridCoordinates.X) == 1
        && Mathf.Abs(current.gridCoordinates.Z - next.gridCoordinates.Z) == 1;

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
    }

    path = tempPath;
    reason = "Success!";
    return true;
  }

  protected override List<GridCell> GetValidGridCells(
    GridObject gridObject,
    GridCell startingGridCell
  )
  {
    GridSystem.Instance.TryGetGridCellsInRange(startingGridCell,new Vector2I(10,3), out List<GridCell> cellsInRange, Enums.GridCellState.Walkable);
    return cellsInRange.Where( cell => Pathfinder.Instance.IsPathPossible(startingGridCell, cell)).ToList();
  }

  public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
  {
	  if (parentGridObject == null)
	  {
		  GD.Print("Parent grid object is null");
		  return (null, 0);
	  }

	  GridCell startingCell = parentGridObject.GridPositionData.GridCell;
	  if (startingCell == null)
	  {
		  GD.Print("Starting grid cell is null");
		  return (null, 0);
	  }

	  float distance = startingCell.worldCenter.DistanceTo(targetGridCell.worldCenter);
    
	  // Normalize distance to a score. Let's say max distance we care about is 20 tiles.
	  float maxDistance = 20.0f;
	  float normalizedScore = (distance / maxDistance) * 70.0f;
    
	  int score = (int)Mathf.Clamp(normalizedScore, 0, 70);
	  return (targetGridCell, score);
  }

  public override bool GetIsUIAction() => true;
  public override string GetActionName() => "Move";
  public override MouseButton GetActionInput() => MouseButton.Left;
  public override bool GetIsAlwaysActive() => true;

  public override bool GetRemainSelected() => true;
}