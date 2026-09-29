using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class DoorNavigationRegression
{
    private static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name).SetValue(target, value);

    private async Task<GridObject> CreateActionUnit(GridCell anchor, Enums.UnitTeam team, int height = 2, int width = 1)
    {
        var unit = new GridObject { gridObjectSettings = Enums.GridObjectSettings.CanWalkThrough };
        var shape = new GridShape { SizeY = height, SizeX = width };
        shape.FillAll(true);
        unit.AddChild(new GridPositionData { Name = "GridPositionData", Shape = shape });
        var stats = new GridObjectStatHolder();
        foreach (var type in new[] { Enums.Stat.Health, Enums.Stat.TimeUnits, Enums.Stat.Stamina })
        {
            var stat = new GridObjectStat();
            SetProperty(stat, "Stat", type);
            SetProperty(stat, "CurrentValue", 100f);
            typeof(GridObjectStat).GetField("maxValue", PrivateInstance).SetValue(stat, 100);
            stats.AddChild(stat);
        }
        unit.AddChild(stats);
        var actions = new GridObjectActions();
        SetProperty(actions, "ActionDefinitions", new ActionDefinition[] { new MoveActionDefinition(), new RotateActionDefinition() });
        unit.AddChild(actions);
        AddChild(unit);
        unit.Position = anchor.WorldCenter;
        await unit.Initialize(team, anchor);
        return unit;
    }

    private static void CheckDoorTargets(GridObject actor, DoorGridObject door, List<GridCell> cells, string placement)
    {
        var action = new interactActionDefinition();
        var anchor = actor.GridPositionData.AnchorCell;
        action.UpdateValidGridCells(actor, anchor);
        var expected = cells.Where(c => c.state.HasFlag(Enums.GridCellState.Ground)).ToHashSet();
        Require(expected.SetEquals(action.ValidGridCells), $"Door highlights include frame/air or omit reachable cells: {placement}");
        var upper = cells.First(c => !c.state.HasFlag(Enums.GridCellState.Ground));
        Require(!action.CanTakeAction(actor, anchor, upper, out _, out _), "Upper door cell accepted by action validation");
        Require(!action.TryBuildCostsOnly(actor, anchor, upper, out _, out _), "Upper door cell accepted by cost preview");
        Require(expected.Contains(action.DetermineBestAIAction().gridCell), "AI did not select a supported interaction cell");
        action.Dispose();
    }

    private async Task CheckObjectActionTargets()
    {
        ResetGrid(1);
        typeof(FirstArrival.Scripts.Managers.GridSystem).GetMethod("BuildAllConnections", PrivateInstance).Invoke(grid, null);
        var actor = await CreateActionUnit(grid.GetGridCell(new Vector3I(10, 1, 12)), Enums.UnitTeam.Player);
        var target = await CreateActionUnit(grid.GetGridCell(new Vector3I(15, 1, 12)), Enums.UnitTeam.Enemy, 4, 3);
        var team = new GridObjectTeamHolder();
        team.TeamVisibleCells.UnionWith(actor.GridPositionData.OccupiedCells);
        team.TeamVisibleCells.UnionWith(target.GridPositionData.OccupiedCells);
        SetProperty(actor, "TeamHolder", team);
        var anchor = actor.GridPositionData.AnchorCell;
        var targetBase = target.GridPositionData.AnchorCell;
        var upper = target.GridPositionData.OccupiedCells.Last();
        var melee = new MeleeAttackActionDefinition { Item = new Item() };
        var support = new ModifyStatActionDefinition { targetStats = new() { { Enums.Stat.Health, -1 } }, timeUnitCost = 1 };
        var ranged = new RangedAttackActionDefinition { range = 10 };
        var ammo = new ItemData();
        SetProperty(ammo, "IsAmmunition", true);
        SetProperty(ammo, "AmmoDamage", 10);
        var weaponData = new ItemData { ActionDefinitions = new() { ranged } };
        SetProperty(weaponData, "AmmoItem", ammo);
        SetProperty(weaponData, "MagazineCapacity", 10);
        ranged.Item = new Item();
        ranged.Item.Init(weaponData);

        foreach (var action in new ActionDefinition[] { melee, support, ranged })
        {
            action.UpdateValidGridCells(actor, anchor);
            Require(action.ValidGridCells.Contains(targetBase), $"{action.GetActionName()} omitted reachable target on raised ground");
            Require(action.ValidGridCells.All(c => c.state.HasFlag(Enums.GridCellState.Ground)), $"{action.GetActionName()} highlighted upper body cells");
            Require(!action.CanTakeAction(actor, anchor, upper, out _, out _), $"{action.GetActionName()} accepted upper cell directly");
        }
        ranged.range = 5;
        ranged.UpdateValidGridCells(actor, anchor);
        Require(ranged.ValidGridCells.Count == 1 && ranged.ValidGridCells[0] == targetBase,
            "Ranged highlighted the out-of-range side of a wide object");
        ranged.range = 1;
        ranged.UpdateValidGridCells(actor, anchor);
        Require(ranged.ValidGridCells.Count == 0, "Ranged highlights an out-of-range target");

        grid.ClearConnectionsForCell(anchor.GridCoordinates);
        grid.MarkNavigationChanged();
        foreach (var action in new ActionDefinition[] { melee, support })
        {
            action.UpdateValidGridCells(actor, anchor);
            Require(!action.ValidGridCells.Contains(targetBase), $"{action.GetActionName()} highlights an unreachable target");
        }
        grid.UpdateConnectionsForCells(new[] { anchor });
        grid.MarkNavigationChanged();
        actor.TryGetGridObjectNode<GridObjectStatHolder>(out var actorStats);
        actorStats.TryGetStat(Enums.Stat.TimeUnits, out var timeUnits);
        SetProperty(timeUnits, "CurrentValue", 0f);
        melee.UpdateValidGridCells(actor, anchor);
        Require(melee.ValidGridCells.Count == 0, "Unaffordable melee action is highlighted");
        SetProperty(timeUnits, "CurrentValue", 100f);
        target.SetIsActive(false);
        melee.UpdateValidGridCells(actor, anchor);
        Require(melee.ValidGridCells.Count == 0, "Inactive object is highlighted");

        // Non-object actions must retain their valid airborne/area cells.
        var turn = new RotateActionDefinition();
        turn.UpdateValidGridCells(actor, anchor);
        Require(turn.ValidGridCells.Count == 1 && turn.ValidGridCells[0] == GridCell.Null, "Shared filtering removed rotation targets");
        GD.Print("PASS: object action highlights, elevated support, reachability, range, affordability and inactive targets.");
        actor.GridPositionData.SetGridCell(null);
        target.GridPositionData.SetGridCell(null);
        actor.Free();
        target.Free();
        team.Free();
        melee.Item.Free();
        ranged.Item.Free();
    }
}
