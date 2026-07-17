using Godot;
using System.Linq;
using System.Collections.Generic;
using BehaviorTree.Core;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

namespace BehaviorTree.Integration;

[GlobalClass]
public partial class BTHasValidTargets : BTCondition
{
    [Export] public ActionDefinition ActionDef { get; set; }
    [Export] public int MinScore { get; set; } = int.MinValue;

    protected override bool Check()
    {
        ActionDefinition actionDef = Blackboard.Has("selectedAction")
            ? Blackboard.Get<ActionDefinition>("selectedAction")
            : ActionDef;

        if (actionDef == null) return false;

        var gridObject = Blackboard.Get<GridObject>("grid_object");
        if (gridObject == null) return false;

        // Prefer the live anchor cell. A previous action can have moved the
        // unit before this behavior tree is evaluated again.
        GridCell startCell = gridObject.GridPositionData?.AnchorCell;

        if (startCell == null && Blackboard.Has("start_cell_coords"))
        {
            Vector3I coords = Blackboard.Get<Vector3I>("start_cell_coords");
            startCell = GridSystem.Instance.GetGridCell(coords);
        }

        if (startCell == null) return false;

        actionDef.parentGridObject = gridObject;

        if (actionDef is ItemActionDefinition itemDef && itemDef.Item == null)
        {
            if (!TryBindItem(gridObject, itemDef)) return false;
        }

        actionDef.UpdateValidGridCells(gridObject, startCell);

        if (actionDef.ValidGridCells == null || actionDef.ValidGridCells.Count == 0)
            return false;

        if (MinScore > int.MinValue)
        {
            return actionDef.ValidGridCells.Any(cell =>
            {
                var (_, score) = actionDef.GetAIActionScore(cell);
                return score >= MinScore;
            });
        }

        return true;
    }

    private bool TryBindItem(GridObject gridObject, ItemActionDefinition itemDef)
    {
        if (!gridObject.TryGetGridObjectNode<GridObjectInventory>(out var inventory)) return false;

        var inventories = new List<InventoryGrid>();
        if (inventory.TryGetInventory(Enums.InventoryType.RightHand, out var rh)) inventories.Add(rh);
        if (inventory.TryGetInventory(Enums.InventoryType.LeftHand, out var lh)) inventories.Add(lh);

        var defType = itemDef.GetType();

        foreach (var inv in inventories)
        {
            foreach (var itemInfo in inv.UniqueItems)
            {
                if (itemInfo.item?.ItemData?.ActionDefinitions == null) continue;
                foreach (var def in itemInfo.item.ItemData.ActionDefinitions)
                {
                    if (def.GetType() == defType)
                    {
                        itemDef.Item = itemInfo.item;
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
