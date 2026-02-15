using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BehaviorTree.Core;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.TurnSystem;

/// <summary>
/// A Turn implementation for AI-controlled teams.
/// Each AI grid object gets its BehaviorTree ticked
/// until it returns Success/Failure (turn over) or
/// all objects have acted.
/// </summary>
[GlobalClass]
public partial class AITurn : Turn
{
    [Export] public Enums.UnitTeam Team { get; set; }

    /// <summary>
    /// Max BT ticks per unit per turn. Safety valve against
    /// infinite RUNNING loops.
    /// </summary>
    [Export] public int MaxTicksPerUnit { get; set; } = 20;

    /// <summary>
    /// Delay between each unit's turn for visual pacing (ms).
    /// </summary>
    [Export] public int DelayBetweenUnitsMs { get; set; } = 200;
	
    protected override async Task _Execute()
    {
	    base._Execute();
        var teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Team);
        if (teamHolder == null)
        {
            GD.PushWarning($"AITurn: No team holder for {Team}");
            TurnManager.Instance.RequestEndOfTurn();
            return;
        }

        var units = teamHolder.GridObjects[Enums.GridObjectState.Active];
        if (units == null || units.Count == 0)
        {
            TurnManager.Instance.RequestEndOfTurn();
            return;
        }

        foreach (var unit in units)
        {
            await ProcessUnit(unit);

            if (DelayBetweenUnitsMs > 0)
            {
                await Task.Delay(DelayBetweenUnitsMs);
            }
        }

        TurnManager.Instance.RequestEndOfTurn();
    }

    private async Task ProcessUnit(GridObject unit)
    {
        // Find the BehaviorTree on this unit
        var bt = unit.GetNodeOrNull<BehaviorTree.Core.BehaviorTree>(
            "BehaviorTree"
        );

        if (bt == null)
        {
            GD.Print($"AITurn: {unit.Name} has no BehaviorTree, skipping.");
            return;
        }

        // Populate the blackboard with current state
        SetupBlackboard(bt.Blackboard, unit);

        int ticks = 0;
        BTStatus status;

        do
        {
            status = bt.TickTree();
            ticks++;

            if (status == BTStatus.Running)
            {
                // Yield a frame so async actions can progress
                await unit.ToSignal(
                    unit.GetTree(),
                    SceneTree.SignalName.ProcessFrame
                );
            }
        } while (status == BTStatus.Running && ticks < MaxTicksPerUnit);

        if (ticks >= MaxTicksPerUnit)
        {
            GD.PushWarning(
                $"AITurn: {unit.Name} hit max ticks ({MaxTicksPerUnit}). "
                + "Possible infinite Running loop."
            );
        }

        GD.Print(
            $"AITurn: {unit.Name} finished with {status} after {ticks} tick(s)."
        );
    }

    private void SetupBlackboard(Blackboard blackboard, GridObject unit)
    {
        // The grid object itself — all BT nodes read this
        blackboard.Set("grid_object", Variant.From(unit));

        // Convenience: cache the start cell
        blackboard.Set(
            "start_cell_coords",
            Variant.From(unit.GridPositionData.AnchorCell.GridCoordinates)
        );

        // Clear any stale per-turn data
        blackboard.Remove("override_target");
        blackboard.Remove("chosen_target_cell");
    }
}