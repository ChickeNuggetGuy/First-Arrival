using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class ExplodeActionDefinition : TurnBasedActionDefinition
{
    [Export] public int turnsUntilExplode = 2;
    [Export] public int explosionRadius = 2;

    public override Action InstantiateAction(
        GridObject parent,
        GridCell startGridCell,
        GridCell targetGridCell,
        Dictionary<Enums.Stat, int> costs
    )
    {
        return new ExplodeAction(
            parent,
            startGridCell,
            targetGridCell,
            this,
            Item,
            costs,
            turnsUntilExplode,
            explosionRadius
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
        if (Item == null)
        {
            reason = "Item not found";
            return false;
        }

        var results = Pathfinder.Instance.TryCalculateArcPath(startingGridCell, targetGridCell);
        if (results.GridCellPath == null || (results.GridCellPath as List<GridCell>)?.Count == 0)
        {
            reason = "No throw path found";
            return false;
        }

        if (!AddRotateCostsIfNeeded(gridObject, startingGridCell, targetGridCell, costs, out var rotateReason))
        {
            reason = rotateReason;
            return false;
        }

        AddCost(costs, Enums.Stat.TimeUnits, 2 * Item.ItemData.weight);
        AddCost(costs, Enums.Stat.Stamina, 8 * Item.ItemData.weight);

        reason = "Success!";
        return true;
    }

    protected override List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell)
    {
        if (!GridSystem.Instance.TryGetGridCellsInRange(
            startingGridCell,
            new Vector2I(5, 3),
            false,
            out List<GridCell> gridCells))
        {
            return new List<GridCell>();
        }

        return gridCells;
    }

    public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
    {
        return (targetGridCell, 50);
    }

    public override bool GetIsUIAction() => true;
    public override string GetActionName() => "Throw Grenade";
    public override MouseButton GetActionInput() => MouseButton.Left;
    public override bool GetIsAlwaysActive() => false;
    public override bool GetRemainSelected() => false;
}