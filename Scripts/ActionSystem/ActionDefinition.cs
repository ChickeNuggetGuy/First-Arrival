using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public abstract partial class ActionDefinition : Resource
{
  public GridObject parentGridObject { get; set; }
  [Export] public bool confirmClick = false;

  public List<GridCell> ValidGridCells { get; protected set; } =
    new List<GridCell>();


  public async Task InstantiateActionCall(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs,
    bool executeAfterCreation = true
  )
  {
    Action action = InstantiateAction(parent, startGridCell, targetGridCell, costs);
    if (executeAfterCreation)
    {
      await action.ExecuteCall();
    }
  }

  public abstract Action InstantiateAction(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs
  );
  
  public bool CanTakeAction(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    out Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    costs = CreateCostContainer();
	
    
    parentGridObject = gridObject;
    if (gridObject == null)
    {
      reason = "GridObject is null";
      costs = CreateFailCosts();
      return false;
    }

    if (!gridObject.TryGetGridObjectNode<GridObjectStatHolder>( out var statholder))
    {
	    reason = "GridObject stat holder is null";
	    costs = CreateFailCosts();
	    return false;
    }
    if (startingGridCell == null || targetGridCell == null)
    {
      reason = "Starting or target grid cell is null";
      costs = CreateFailCosts();
      return false;
    }

    // Let subclass validate and build costs
    if (
      !OnValidateAndBuildCosts(
        gridObject,
        startingGridCell,
        targetGridCell,
        costs,
        out reason
      )
    )
    {
      // Normalize fail costs for consistent UI/feedback
      costs = CreateFailCosts();
      return false;
    }

    // Final affordability check is always last
    if (!statholder.CanAffordStatCost(costs))
    {
      reason = "Can't afford stat costs";
      return false;
    }

    if (string.IsNullOrWhiteSpace(reason)) reason = "Success!";
    return true;
  }
  
  public bool TryBuildCostsOnly(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    out Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    costs = CreateCostContainer();

    if (gridObject == null)
    {
      reason = "GridObject is null";
      return false;
    }

    if (startingGridCell == null || targetGridCell == null)
    {
      reason = "Starting or target grid cell is null";
      return false;
    }

    return OnValidateAndBuildCosts(
      gridObject,
      startingGridCell,
      targetGridCell,
      costs,
      out reason
    );
  }
  
  protected abstract bool OnValidateAndBuildCosts(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs,
    out string reason
  );

  public void UpdateValidGridCells(GridObject gridObject, GridCell startingGridCell)
  {
    ValidGridCells.Clear();
    ValidGridCells.AddRange(GetValidGridCells(gridObject, startingGridCell));
  }

  protected abstract List<GridCell> GetValidGridCells(
    GridObject gridObject,
    GridCell startingGridCell
  );

  public (GridCell gridCell, int score, Dictionary<Enums.Stat, int> costs) DetermineBestAIAction()
  {
	  List<GridCell> possibleGridCells = GetValidGridCells(parentGridObject, parentGridObject.GridPositionData.AnchorCell);
	  GD.Print($"{GetActionName()}: Possible grid cells: {possibleGridCells.Count}");
	  if (possibleGridCells.Count == 0)
	  {
		  return (null, int.MinValue, null);
	  }

	  var gridCellScores = new List<(GridCell gridCell, int score, Dictionary<Enums.Stat, int> costs)>();

	  foreach (var possibleGridCell in possibleGridCells)
	  {
		  if (!CanTakeAction(parentGridObject, parentGridObject.GridPositionData.AnchorCell, possibleGridCell, out var costs, out _))
		  {
			  continue;
		  }
		  var result = GetAIActionScore(possibleGridCell);
		  gridCellScores.Add((result.gridCell, result.score, costs));
	  }

	  if (!gridCellScores.Any())
	  {
		  return (null, int.MinValue, null);
	  }

	  gridCellScores.Sort((a, b) => b.score.CompareTo(a.score));
	  return gridCellScores.First();
  }
  public abstract (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell);
  public abstract bool GetIsUIAction();

  public abstract string GetActionName();

  public abstract MouseButton GetActionInput();

  public abstract bool GetIsAlwaysActive();

  public abstract bool GetRemainSelected();

  

  protected Dictionary<Enums.Stat, int> CreateCostContainer()
  {
    return new Dictionary<Enums.Stat, int>
    {
      { Enums.Stat.TimeUnits, 0 },
      { Enums.Stat.Stamina, 0 }
    };
  }

  protected Dictionary<Enums.Stat, int> CreateFailCosts()
  {
    return new Dictionary<Enums.Stat, int>
    {
      { Enums.Stat.TimeUnits, -1 },
      { Enums.Stat.Stamina, -1 }
    };
  }

  protected static void AddCost(
    Dictionary<Enums.Stat, int> target,
    Enums.Stat stat,
    int value
  )
  {
    if (!target.ContainsKey(stat)) target[stat] = 0;
    target[stat] += value;
  }

  protected static void AddCosts(
    Dictionary<Enums.Stat, int> target,
    Dictionary<Enums.Stat, int> add
  )
  {
    if (add == null) return;
    foreach (var kv in add)
    {
      AddCost(target, kv.Key, kv.Value);
    }
  }

  // Adds default rotate costs if direction differs. Returns false if rotation
  // isn't possible
  protected bool AddRotateCostsIfNeeded(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    reason = "";
    if (!gridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions))
    {
	    reason = "Grid object action not found";
	    return false;
    }
    
    bool hasRotate =
	    gridObjectActions.ActionDefinitions?.Any(a => a is RotateActionDefinition) ?? false;
    if (!hasRotate)
    {
      reason = "Rotate action not found";
      return false;
    }

    var currentDir = gridObject.GridPositionData.Direction;
    var targetDir = RotationHelperFunctions.GetDirectionBetweenCells(
      startingGridCell,
      targetGridCell
    );

    if (currentDir == targetDir) return true;

    int steps = Mathf.Abs(
      RotationHelperFunctions.GetRotationStepsBetweenDirections(currentDir, targetDir)
    );

    // Default rotation cost: 1 TU + 1 Stamina per step (matches RotateActionDefinition)
    AddCost(costs, Enums.Stat.TimeUnits, steps * 1);
    AddCost(costs, Enums.Stat.Stamina, steps * 1);

    return true;
  }
}