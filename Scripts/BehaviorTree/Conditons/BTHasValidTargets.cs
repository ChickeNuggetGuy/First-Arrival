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
        if (ActionDef == null) return false;

        var gridObject = Blackboard.Get<GridObject>("grid_object");
        if (gridObject == null) return false;

        GridCell startCell = null;

        // Resolve Coords
        if (Blackboard.Has("start_cell_coords"))
        {
            Vector3I coords = Blackboard.Get<Vector3I>("start_cell_coords");
            startCell = GridSystem.Instance.GetGridCell(coords);
        }

        if (startCell == null) return false;

        ActionDef.parentGridObject = gridObject;

        if (ActionDef is ItemActionDefinition itemDef && itemDef.Item == null)
        {
            if (!TryBindItem(gridObject, itemDef)) return false;
        }

        ActionDef.UpdateValidGridCells(gridObject, startCell);

        if (ActionDef.ValidGridCells == null || ActionDef.ValidGridCells.Count == 0)
            return false;

        if (MinScore > int.MinValue)
        {
            return ActionDef.ValidGridCells.Any(cell =>
            {
                var (_, score) = ActionDef.GetAIActionScore(cell);
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

        var defType = ActionDef.GetType();

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