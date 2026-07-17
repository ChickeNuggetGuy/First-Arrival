using Godot;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MoveActionDefinition : ActionDefinition
{
  private const int OrthogonalTimeUnitCost = 4;
  private const int DiagonalTimeUnitCost = 6;
  private const int MovementStaminaCost = 2;
  private const int RotationCostPerStep = 1;

  public List<GridCell> path = new List<GridCell>();

  public override ActionBase InstantiateAction(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Godot.Collections.Dictionary<Enums.Stat, int> costs
  )
  {
    return new MoveActionBase(parent, startGridCell, targetGridCell, this, costs);
  }

  protected override bool OnValidateAndBuildCosts(
  GridObject gridObject,
  GridCell startingGridCell,
  GridCell targetGridCell,
  Godot.Collections.Dictionary<Enums.Stat, int> costs,
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
  var start = gs.GetGridCell(startingGridCell.GridCoordinates);
  var goal = gs.GetGridCell(targetGridCell.GridCoordinates);

  if (start == null || goal == null)
  {
    reason = "Start or target out of grid bounds";
    return false;
  }

  if (!goal.IsWalkable || goal.HasMovementBlockingGridObject())
  {
    reason = "Target grid cell is not walkable or is occupied";
    return false;
  }
  

  var tempPath = Pathfinder.Instance.FindPath(start, goal);
  if (tempPath == null || tempPath.Count == 0)
  {
    reason = "No path found";
    return false;
  }

  // Cost simulation (same logic you already had)
  var facing = gridObject.GridPositionData.Direction;

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
      AddCost(costs, Enums.Stat.TimeUnits, Mathf.Abs(steps) * RotationCostPerStep);
      AddCost(costs, Enums.Stat.Stamina, Mathf.Abs(steps) * RotationCostPerStep);
      facing = stepDir;
    }

    bool diagonal =
      Mathf.Abs(current.GridCoordinates.X - next.GridCoordinates.X) == 1 &&
      Mathf.Abs(current.GridCoordinates.Z - next.GridCoordinates.Z) == 1;

    if (diagonal)
    {
      AddCost(costs, Enums.Stat.TimeUnits, DiagonalTimeUnitCost);
      AddCost(costs, Enums.Stat.Stamina, MovementStaminaCost);
    }
    else
    {
      AddCost(costs, Enums.Stat.TimeUnits, OrthogonalTimeUnitCost);
      AddCost(costs, Enums.Stat.Stamina, MovementStaminaCost);
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
    var validCells = new List<GridCell>();
    if (
      gridObject == null
      || startingGridCell == null
      || GridSystem.Instance == null
      || Pathfinder.Instance == null
      || !gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder)
      || !statHolder.TryGetStat(Enums.Stat.TimeUnits, out var timeUnits)
      || !statHolder.TryGetStat(Enums.Stat.Stamina, out var stamina)
    )
      return validCells;

    // Every move consumes at least these base costs. This gives us a safe
    // step limit for collecting candidates before calculating their exact
    // path costs (including diagonals and rotations).
    int maxSteps = Mathf.FloorToInt(Mathf.Min(
      timeUnits.CurrentValue / OrthogonalTimeUnitCost,
      stamina.CurrentValue / MovementStaminaCost
    ));
    if (maxSteps <= 0)
      return validCells;

    var candidates = GetCellsWithinStepLimit(startingGridCell, maxSteps);
    foreach (GridCell candidate in candidates)
    {
      // CanTakeAction uses the same path-cost simulation as execution and
      // performs the final affordability check against both current stats.
      if (CanTakeAction(
        gridObject,
        startingGridCell,
        candidate,
        out _,
        out _
      ))
        validCells.Add(candidate);
    }

    return validCells;
  }

  private static List<GridCell> GetCellsWithinStepLimit(
    GridCell startingGridCell,
    int maxSteps
  )
  {
    var result = new List<GridCell>();
    var visited = new HashSet<GridCell> { startingGridCell };
    var frontier = new Queue<(GridCell Cell, int Steps)>();
    frontier.Enqueue((startingGridCell, 0));

    while (frontier.Count > 0)
    {
      var (cell, steps) = frontier.Dequeue();
      if (steps >= maxSteps)
        continue;

      if (!GridSystem.Instance.TryGetGridCellNeighbors(
        cell,
        true,
        false,
        out var neighbors
      ))
        continue;

      foreach (GridCell neighbor in neighbors)
      {
        if (neighbor == null ||
            neighbor.HasMovementBlockingGridObject() ||
            !visited.Add(neighbor))
          continue;

        result.Add(neighbor);
        frontier.Enqueue((neighbor, steps + 1));
      }
    }

    return result;
  }

  public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
  {
	  if (parentGridObject == null)
	  {
		  GD.Print("Parent grid object is null");
		  return (null, 0);
	  }
	  
	  if (!parentGridObject.TryGetGridObjectNode<GridObjectSight>(out var sightArea)) return (null, 0);

	  GridCell startingCell = parentGridObject.GridPositionData.AnchorCell;
	  if (startingCell == null)
	  {
		  GD.Print("Starting grid cell is null");
		  return (null, 0);
	  }

	  if (targetGridCell.HasGridObject())
		  return (targetGridCell, 0);

	
	  
	  if (sightArea == null) return (targetGridCell, 0);
	  
	  float nearestEnemyDistance = float.MaxValue;
	  foreach (GridObject seenObject in sightArea.SeenGridObjects)
	  {
		  if (
			  seenObject == null
			  || seenObject == parentGridObject
			  || !seenObject.IsActive
			  || seenObject.scenery
			  || seenObject.Team == parentGridObject.Team
			  || seenObject.GridPositionData?.AnchorCell == null
		  )
			  continue;

		  float enemyDistance = targetGridCell.WorldCenter.DistanceTo(
			  seenObject.GridPositionData.AnchorCell.WorldCenter
		  );
		  nearestEnemyDistance = Mathf.Min(nearestEnemyDistance, enemyDistance);
	  }

	  if (nearestEnemyDistance < float.MaxValue)
	  {
		  // DetermineBestAIAction chooses the highest score, so cells closer to
		  // a visible enemy are preferred over arbitrary reachable cells.
		  return (targetGridCell, 1000 - Mathf.RoundToInt(nearestEnemyDistance * 10.0f));
	  }

	  
	  float distance = startingCell.WorldCenter.DistanceTo(targetGridCell.WorldCenter);
	  
	  float maxDistance = 40.0f;
	  float normalizedScore = (distance / maxDistance) * 45.0f;

	  // Search movement should vary its destination, but longer moves retain
	  // a meaningful advantage over nearby cells.
	  int score = (int)Mathf.Clamp(normalizedScore, 0, 45);
	  score += GD.RandRange(0, 40);
	  return (targetGridCell, score);
  }

  public override bool GetIsUIAction() => true;
  public override string GetActionName() => "Move";
  public override MouseButton GetActionInput() => MouseButton.Left;
  public override bool GetIsAlwaysActive() => true;

  public override bool GetRemainSelected() => true;
}
