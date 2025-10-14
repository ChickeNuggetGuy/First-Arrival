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
  var gs = GridSystem.Instance;
  if (gs == null)
  {
    reason = "GridSystem not initialized";
    return false;
  }

  // Rebind to canonical instances in the grid
  var start = gs.GetGridCell(startingGridCell.gridCoordinates);
  var goal = gs.GetGridCell(targetGridCell.gridCoordinates);

  if (start == null || goal == null)
  {
    reason = "Start or target out of grid bounds";
    return false;
  }

  if (!goal.IsWalkable)
  {
    reason = "Target grid cell is not walkable";
    return false;
  }

  if (!start.IsWalkable)
  {
    reason = "Starting grid cell is not walkable";
    GD.Print(
    );
    return false;
  }

  var tempPath = Pathfinder.Instance.FindPath(start, goal);
  if (tempPath == null || tempPath.Count == 0)
  {
    reason = "No path found";
    return false;
  }

  // Cost simulation (same logic you already had)
  var facing = RotationHelperFunctions.GetDirectionFromRotation3D(
    gridObject.Rotation.Y
  );

  for (int i = 0; i < tempPath.Count - 1; i++)
  {
    var current = tempPath[i];
    var next = tempPath[i + 1];

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
      Mathf.Abs(current.gridCoordinates.X - next.gridCoordinates.X) == 1 &&
      Mathf.Abs(current.gridCoordinates.Z - next.gridCoordinates.Z) == 1;

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
    GridSystem.Instance.TryGetGridCellsInRange(startingGridCell,new Vector2I(20,3),true, out List<GridCell> cellsInRange);
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

	  // if (targetGridCell.HasGridObject())
		  return (targetGridCell, 0);
	  
	  GridObjectSight sightArea = parentGridObject.gridObjectNodesDictionary["all"].FirstOrDefault(node => node is GridObjectSight) as GridObjectSight;
	  if (sightArea == null) return (targetGridCell, 0);
	  
	  if(sightArea.SeenGridObjects.Count > 0)
	  {
		  GD.Print("Can seen grid objects");
		  return (targetGridCell, 0);
	  }

	  
	  float distance = startingCell.worldCenter.DistanceTo(targetGridCell.worldCenter);
	  
	  float maxDistance = 40.0f;
	  float normalizedScore = (distance / maxDistance) * 70.0f;
    
	  int score = (int)Mathf.Clamp(normalizedScore, 0, 70);
	  score += GD.RandRange(0, 5);
	  return (targetGridCell, score);
  }

  public override bool GetIsUIAction() => true;
  public override string GetActionName() => "Move";
  public override MouseButton GetActionInput() => MouseButton.Left;
  public override bool GetIsAlwaysActive() => true;

  public override bool GetRemainSelected() => true;
}