using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BehaviorTree.Core;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

namespace BehaviorTree.Integration;

[GlobalClass]
public partial class BTExecuteAction : BTAsyncBridge
{
    [Export] public bool UseScoring { get; set; } = true;
    [Export] public int MinScoreThreshold { get; set; } = int.MinValue;
    
    [Export] public string TargetCoordsBlackboardKey { get; set; } = "chosen_target_coords";
    [Export] public bool AutoBindItem { get; set; } = true;

    private GridObject _gridObject;
    private GridCell _startCell;

    protected override void OnEnter()
    {
        base.OnEnter();
        _gridObject = null;
        _startCell = null;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        // 1. Get the action to perform (set by previous nodes like BTHasActionDefinition)
        if (!Blackboard.Has("selectedAction")) return false;
        ActionDefinition actionDef = Blackboard.Get<ActionDefinition>("selectedAction");
        
        _gridObject = Blackboard.Get<GridObject>("grid_object");
        if (_gridObject == null) return false;

        // 2. Resolve Start Cell
        if (Blackboard.Has("start_cell_coords"))
        {
            Vector3I startCoords = Blackboard.Get<Vector3I>("start_cell_coords");
            _startCell = GridSystem.Instance.GetGridCell(startCoords);
        }
        
        if (_startCell == null) 
            _startCell = _gridObject.GridPositionData.AnchorCell;

        if (_startCell == null) return false;

        actionDef.parentGridObject = _gridObject;

        // 3. Auto-Bind Items (e.g. Grenade Item -> Grenade Action)
        if (AutoBindItem && actionDef is ItemActionDefinition itemActionDef && itemActionDef.Item == null)
        {
            if (!TryBindItem(itemActionDef, actionDef)) return false;
        }

        GridCell targetCell = null;
        Dictionary<Enums.Stat, int> costs = null;

        // 4. Resolve Target (Override vs Best Score)
        if (Blackboard.Has("override_target_coords"))
        {
            Vector3I overrideCoords = Blackboard.Get<Vector3I>("override_target_coords");
            var overrideCell = GridSystem.Instance.GetGridCell(overrideCoords);

            if (overrideCell != null)
            {
                if (actionDef.CanTakeAction(_gridObject, _startCell, overrideCell, out costs, out string reason))
                {
                    targetCell = overrideCell;
                }
                else
                {
                    GD.Print($"BTExecuteAction: Override target {overrideCoords} invalid: {reason}");
                    return false;
                }
            }
        }
        
        if (targetCell == null)
        {
            var found = FindBestTarget(actionDef);
            if (found == null) return false;

            targetCell = found.Value.cell;
            costs = found.Value.costs;
        }

        if (!string.IsNullOrEmpty(TargetCoordsBlackboardKey))
        {
            Blackboard.Set(TargetCoordsBlackboardKey, Variant.From(targetCell.GridCoordinates));
        }

        // 5. Execute and Wait for SPECIFIC Completion
        bool accepted = await ActionManager.Instance.TryTakeAction(
            actionDef, 
            _gridObject, 
            _startCell, 
            targetCell
        );

        if (accepted)
        {
            while (true)
            {
                var signalArgs = await ActionManager.Instance.ToSignal(
                    ActionManager.Instance, 
                    ActionManager.SignalName.ActionCompleted
                );

                var completedActionDef = signalArgs[0].As<ActionDefinition>();

                if (completedActionDef == actionDef)
                {
                    // The root action we started finished.
                    break;
                }
                
                // If we are here, a sub-action finished. We keep waiting.
            }
        }

        return accepted;
    }

    private (GridCell cell, Dictionary<Enums.Stat, int> costs)? FindBestTarget(ActionDefinition actionDef)
    {
        if (UseScoring)
        {
            var (gridCell, score, costs) = actionDef.DetermineBestAIAction();
            if (gridCell == null || score < MinScoreThreshold) return null;
            return (gridCell, costs);
        }

        actionDef.UpdateValidGridCells(_gridObject, _startCell);
        var validCells = actionDef.ValidGridCells;

        if (validCells == null || validCells.Count == 0) return null;

        var sorted = validCells.OrderBy(c => _startCell.GridCoordinates.DistanceSquaredTo(c.GridCoordinates));

        foreach (var cell in sorted)
        {
            if (actionDef.CanTakeAction(_gridObject, _startCell, cell, out var costs, out _))
            {
                return (cell, costs);
            }
        }

        return null;
    }

    private bool TryBindItem(ItemActionDefinition itemActionDef, ActionDefinition actionDef)
    {
        if (!_gridObject.TryGetGridObjectNode<GridObjectInventory>(out var inventory)) return false;

        var inventories = new List<InventoryGrid>();
        if (inventory.TryGetInventory(Enums.InventoryType.RightHand, out var rh)) inventories.Add(rh);
        if (inventory.TryGetInventory(Enums.InventoryType.LeftHand, out var lh)) inventories.Add(lh);

        var defType = actionDef.GetType();

        foreach (var inv in inventories)
        {
            foreach (var itemInfo in inv.UniqueItems)
            {
                if (itemInfo.item?.ItemData?.ActionDefinitions == null) continue;
                foreach (var def in itemInfo.item.ItemData.ActionDefinitions)
                {
                    if (def.GetType() == defType)
                    {
                        itemActionDef.Item = itemInfo.item;
                        return true;
                    }
                }
            }
        }
        return false;
    }
}