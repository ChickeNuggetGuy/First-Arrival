using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Managers;

public partial class GridObjectManager
{
    // Resolve after ALL teams and ground inventories load: a body may be carried
    // by a member of another team. Unit data is saved once in its team roster.
    public void RestoreBodyLinks()
    {
        var units = gridObjectTeams.Values.SelectMany(holder => holder.GridObjects.Values)
            .SelectMany(list => list).Distinct().ToArray();
        var byId = units.ToDictionary(unit => unit.UnitId);
        var inventories = new HashSet<InventoryGrid>();
        foreach (GridCell cell in GridSystem.Instance?.AllGridCells ?? System.Array.Empty<GridCell>())
            if (cell?.InventoryGrid != null) inventories.Add(cell.InventoryGrid);
        foreach (var unit in units)
        {
            if (!unit.TryGetGridObjectNode<GridObjectInventory>(out var inventory)) continue;
            foreach (InventoryGrid grid in inventory.InventoryGrids.Values) inventories.Add(grid);
        }
        var linked = new HashSet<string>();
        foreach (InventoryGrid grid in inventories)
        {
            foreach (UnitBodyItem body in grid.UniqueItems.Select(entry => entry.item).OfType<UnitBodyItem>().ToArray())
            {
                if (!byId.TryGetValue(body.UnitId, out var unit) || unit.Condition == null ||
                    unit.Condition.State == Enums.UnitCondition.Conscious || !linked.Add(body.UnitId))
                {
                    GD.PushWarning($"Removing orphaned or duplicate body for {body.UnitName}.");
                    grid.TryRemoveItem(body, 1);
                    body.QueueFree();
                    continue;
                }
                unit.Condition.AttachBody(body);
            }
        }
    }
}
