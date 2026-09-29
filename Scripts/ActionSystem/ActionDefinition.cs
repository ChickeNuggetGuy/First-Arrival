using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public abstract partial class ActionDefinition : Resource
{
  // Object actions select supported cells, then apply their own range,
  // movement and cost checks. Cell/area actions keep their existing rules.
  protected virtual bool TargetsGridObjects => false;

  private bool IsSelectableTargetCell(GridCell cell)
  {
    return !TargetsGridObjects ||
      (cell != null && cell != GridCell.Null &&
       cell.state.HasFlag(Enums.GridCellState.Ground) &&
       !cell.state.HasFlag(Enums.GridCellState.Disabled) && cell.HasGridObject());
  }

  public GridObject parentGridObject { get; set; }
  [Export] public bool confirmClick = false;
  

  public List<GridCell> ValidGridCells { get; protected set; } =
    new List<GridCell>();


  public async Task InstantiateActionCall(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Godot.Collections.Dictionary<Enums.Stat, int> costs,
    bool executeAfterCreation = true
  )
  {
    ActionBase actionBase = InstantiateAction(parent, startGridCell, targetGridCell, costs);
    if (executeAfterCreation)
    {
      await actionBase.ExecuteCall();
    }
  }

  public abstract ActionBase InstantiateAction(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Godot.Collections.Dictionary<Enums.Stat, int> costs
  );
  
  public bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell,
    out  Godot.Collections.Dictionary<Enums.Stat, int> costs, out string reason)
  {
    costs = CreateCostContainer();
	
    
    parentGridObject = gridObject;
    if (gridObject == null || !gridObject.CanAct)
    {
      reason = "Unit cannot act";
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

    if (!IsSelectableTargetCell(targetGridCell))
    {
      reason = "Target must be a ground-supported object cell";
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
    out  Godot.Collections.Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    costs = CreateCostContainer();

    if (gridObject == null || !gridObject.CanAct)
    {
      reason = "Unit cannot act";
      return false;
    }

    if (startingGridCell == null || targetGridCell == null)
    {
      reason = "Starting or target grid cell is null";
      return false;
    }

    parentGridObject = gridObject;
    if (!IsSelectableTargetCell(targetGridCell))
    {
      reason = "Target must be a ground-supported object cell";
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
    Godot.Collections.Dictionary<Enums.Stat, int> costs,
    out string reason
  );

  public void UpdateValidGridCells(GridObject gridObject, GridCell startingGridCell)
  {
    parentGridObject = gridObject;
    ValidGridCells.Clear();
    List<GridCell> validCells = GetSelectableGridCells(gridObject, startingGridCell);
    if (validCells != null)
      ValidGridCells.AddRange(validCells);
  }

  private List<GridCell> GetSelectableGridCells(GridObject gridObject, GridCell startingGridCell)
  {
    if (gridObject == null || !gridObject.CanAct || startingGridCell == null)
      return new List<GridCell>();

    parentGridObject = gridObject;
    var candidates = GetValidGridCells(gridObject, startingGridCell);
    if (candidates == null) return new List<GridCell>();
    if (!TargetsGridObjects) return candidates;

    return candidates.Where(IsSelectableTargetCell).Distinct()
      .Where(cell => CanTakeAction(gridObject, startingGridCell, cell, out _, out _))
      .ToList();
  }

  protected abstract List<GridCell> GetValidGridCells(
    GridObject gridObject,
    GridCell startingGridCell
  );

  public (GridCell gridCell, int score,  Godot.Collections.Dictionary<Enums.Stat, int> costs) DetermineBestAIAction()
  {
	  List<GridCell> possibleGridCells = GetSelectableGridCells(parentGridObject, parentGridObject?.GridPositionData?.AnchorCell);
	  GD.Print($"{GetActionName()}: Possible grid cells: {possibleGridCells.Count}");
	  if (possibleGridCells.Count == 0)
	  {
		  return (null, int.MinValue, null);
	  }

	  var gridCellScores = new List<(GridCell gridCell, int score,  Godot.Collections.Dictionary<Enums.Stat, int> costs)>();

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

  

  protected  Godot.Collections.Dictionary<Enums.Stat, int> CreateCostContainer()
  {
    return new  Godot.Collections.Dictionary<Enums.Stat, int>
    {
      { Enums.Stat.TimeUnits, 0 },
      { Enums.Stat.Stamina, 0 }
    };
  }

  protected  Godot.Collections.Dictionary<Enums.Stat, int> CreateFailCosts()
  {
    return new  Godot.Collections.Dictionary<Enums.Stat, int>
    {
      { Enums.Stat.TimeUnits, -1 },
      { Enums.Stat.Stamina, -1 }
    };
  }

  protected static void AddCost(
	  Godot.Collections.Dictionary<Enums.Stat, int> target,
    Enums.Stat stat,
    int value
  )
  {
    if (!target.ContainsKey(stat)) target[stat] = 0;
    target[stat] += value;
  }

  protected static void AddCosts(Godot.Collections.Dictionary<Enums.Stat, int> target,
    Godot.Collections.Dictionary<Enums.Stat, int> add
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
    Godot.Collections.Dictionary<Enums.Stat, int> costs,
    out string reason,
    GridCell occupancyAnchor = null,
    Enums.Direction assumedCurrentDirection = Enums.Direction.None
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

    var currentDir = assumedCurrentDirection == Enums.Direction.None
      ? gridObject.GridPositionData.Direction
      : assumedCurrentDirection;
    var targetDir = RotationHelperFunctions.GetDirectionBetweenCells(
      startingGridCell,
      targetGridCell
    );

    if (targetDir == Enums.Direction.None)
      return true;

    GridCell finalAnchor = occupancyAnchor
      ?? gridObject.GridPositionData.AnchorCell
      ?? startingGridCell;
    if (!gridObject.GridPositionData.CanOccupyAt(
          finalAnchor,
          targetDir,
          out string footprintReason
        ))
    {
      reason = $"Cannot face target: {footprintReason}";
      return false;
    }

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
