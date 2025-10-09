using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.ActionSystem.ItemActions.ThrowAction;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class ThrowActionDefinition : ActionDefinition, IItemActionDefinition
{
  public Item Item { get; set; }

  private List<GridCell> _path = new List<GridCell>();
  private Vector3[] _vectorPath;

  public override Action InstantiateAction(
    GridObject parent,
    GridCell startGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs
  )
  {
    GD.Print($"Vector path {(_vectorPath?.Length ?? 0)} path: {(_path?.Count ?? 0)}");
    // If your ThrowAction constructor accepts Item, keep this.
    // If not, remove Item here and set the action.Item manually after instantiation.
    return new ThrowAction(parent, startGridCell, targetGridCell, this, Item, _path, _vectorPath, costs);
  }

  protected override bool OnValidateAndBuildCosts(
    GridObject gridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    Dictionary<Enums.Stat, int> costs,
    out string reason
  )
  {
    if (Item == null)
    {
      reason = "Item not found";
      return false;
    }

    var results = Pathfinder.Instance.TryCalculateArcPath(startingGridCell, targetGridCell);

    _path = (List<GridCell>)results.GridCellPath;
    _vectorPath = results.Vector3Path?.ToArray();

    if (_path == null || _path.Count == 0)
    {
      reason = "No path found";
      return false;
    }

    // Add rotate costs if orientation is needed
    if (
      !AddRotateCostsIfNeeded(
        gridObject,
        startingGridCell,
        targetGridCell,
        costs,
        out var rotateReason
      )
    )
    {
      reason = rotateReason;
      return false;
    }

    // Base throw costs (example based on item weight)
    AddCost(costs, Enums.Stat.TimeUnits, 2 * Item.ItemData.weight);
    AddCost(costs, Enums.Stat.Stamina, 8 * Item.ItemData.weight);

    reason = "Success!";
    return true;
  }

  protected override List<GridCell> GetValidGridCells(
    GridObject gridObject,
    GridCell startingGridCell
  )
  {
    if (
      !GridSystem.Instance.TryGetGridCellsInRange(
        startingGridCell,
        new Vector2I(5, 3),
        false,
        out List<GridCell> gridCells
      )
    )
    {
      return null;
    }

    return gridCells;
  }

  public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
  {
	  return (targetGridCell, 0);
  }
  public override bool GetIsUIAction() => true;
  public override string GetActionName() => "Throw";
  public override MouseButton GetActionInput() => MouseButton.Left;
  public override bool GetIsAlwaysActive() => false;
  public override bool GetRemainSelected() => false;
}