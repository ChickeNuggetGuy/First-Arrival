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

	private Dictionary<Vector3I, Pathfinder.FootprintPathResult> _reachablePathCache = new();
	private GridObject _cachedGridObject;
	private Vector3I _cachedStartCoordinates;
	private Enums.Direction _cachedDirection = Enums.Direction.None;
	private ulong _cachedNavigationRevision = ulong.MaxValue;
	private int _cachedTimeUnits = -1;
	private int _cachedStamina = -1;

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

	  if (!TryGetReachablePath(gridObject, start, goal, out var pathResult))
	  {
	    reason = "No affordable path can fit the unit's complete footprint";
	    return false;
	  }

	  AddCost(costs, Enums.Stat.TimeUnits, pathResult.TimeUnitCost);
	  AddCost(costs, Enums.Stat.Stamina, pathResult.StaminaCost);
	  path = pathResult.Path;
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

	    EnsureReachabilityCache(
	      gridObject,
	      startingGridCell,
	      Mathf.FloorToInt(timeUnits.CurrentValue),
	      Mathf.FloorToInt(stamina.CurrentValue)
	    );

	    foreach (Vector3I coordinates in _reachablePathCache.Keys)
	    {
	      GridCell cell = GridSystem.Instance.GetGridCell(coordinates);
	      if (cell != null) validCells.Add(cell);
	    }

	    return validCells;
	  }

	private bool TryGetReachablePath(
		GridObject gridObject,
		GridCell start,
		GridCell goal,
		out Pathfinder.FootprintPathResult result
	)
	{
		result = null;
		if (!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder) ||
		    !statHolder.TryGetStat(Enums.Stat.TimeUnits, out var timeUnits) ||
		    !statHolder.TryGetStat(Enums.Stat.Stamina, out var stamina))
			return false;

		EnsureReachabilityCache(
			gridObject,
			start,
			Mathf.FloorToInt(timeUnits.CurrentValue),
			Mathf.FloorToInt(stamina.CurrentValue)
		);
		return _reachablePathCache.TryGetValue(goal.GridCoordinates, out result);
	}

	private void EnsureReachabilityCache(
		GridObject gridObject,
		GridCell start,
		int timeUnits,
		int stamina
	)
	{
		GridSystem gridSystem = GridSystem.Instance;
		if (gridSystem == null || Pathfinder.Instance == null ||
		    gridObject?.GridPositionData == null || start == null)
		{
			_reachablePathCache.Clear();
			return;
		}

		Enums.Direction direction = gridObject.GridPositionData.Direction;
		if (_cachedGridObject == gridObject &&
		    _cachedStartCoordinates == start.GridCoordinates &&
		    _cachedDirection == direction &&
		    _cachedNavigationRevision == gridSystem.NavigationRevision &&
		    _cachedTimeUnits == timeUnits &&
		    _cachedStamina == stamina)
			return;

		_reachablePathCache = Pathfinder.Instance.FindReachablePathsForGridObject(
			gridObject,
			start,
			Mathf.Max(0, timeUnits),
			Mathf.Max(0, stamina)
		);
		_cachedGridObject = gridObject;
		_cachedStartCoordinates = start.GridCoordinates;
		_cachedDirection = direction;
		_cachedNavigationRevision = gridSystem.NavigationRevision;
		_cachedTimeUnits = timeUnits;
		_cachedStamina = stamina;
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
