using Godot;
using System.Linq;
using System.Threading.Tasks;
using BehaviorTree.Core;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.AI;

[GlobalClass]
public partial class BTActionSegment : TurnSegment
{
    [Export] private int _maxTicksPerUnit = 50;
    
    [Export] private int _delayBetweenUnitsMs = 500; 

    private GridObjectTeamHolder _teamHolder;

    protected override Task _Setup()
    {
        _teamHolder = GridObjectManager.Instance
            .GetGridObjectTeamHolder(parentTurn.team);
        return Task.CompletedTask;
    }

    protected override async Task _Execute()
    {
        if (_teamHolder == null) return;

        // Get all units that are active and have actions
        var activeUnits = _teamHolder
            .GridObjects[Enums.GridObjectState.Active]
            .Where(go =>
                go.TryGetGridObjectNode<GridObjectActions>(out var actions)
                && actions.ActionDefinitions.Length > 0
            )
            .ToArray();

        GD.Print($"BTActionSegment: Starting turn for {activeUnits.Length} units.");

        foreach (var unit in activeUnits)
        {
            CameraController.Instance.FocusOn(unit); 

            await ProcessUnitUntilFinished(unit);

            if (_delayBetweenUnitsMs > 0) 
            {
                await Task.Delay(_delayBetweenUnitsMs);
            }
        }
        
        GD.Print("BTActionSegment: All units finished.");
    }

    private async Task ProcessUnitUntilFinished(GridObject unit)
    {
        var bt = unit.GetNodeOrNull<BehaviorTree.Core.BehaviorTree>("BehaviorTree");
        if (bt == null) return;

        GD.Print($"---> AI Turn: {unit.Name} Starting");

        SetupBlackboard(bt.Blackboard, unit);

        int ticks = 0;
        BTStatus status;

        // Keep ticking this specific unit until it succeeds or fails
        do
        {
            status = bt.TickTree();
            ticks++;

            if (status == BTStatus.Running)
            {
                // Wait for the next visual frame.
                await unit.ToSignal(unit.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

        } while (status == BTStatus.Running && ticks < _maxTicksPerUnit);

        // Safety cleanup
        if (ticks >= _maxTicksPerUnit) 
        {
            GD.PushWarning($"AI Turn: {unit.Name} timed out after {_maxTicksPerUnit} ticks.");
            // Force one last tick to allow cleanup/abort
            bt.TickTree(); 
        }

        GD.Print($"<--- AI Turn: {unit.Name} Finished ({status})");
    }

    private void SetupBlackboard(Blackboard blackboard, GridObject unit)
    {
        blackboard.Set("grid_object", Variant.From(unit));

        if (unit.GridPositionData?.AnchorCell != null)
        {
            blackboard.Set(
                "start_cell_coords", 
                Variant.From(unit.GridPositionData.AnchorCell.GridCoordinates)
            );
        }

        blackboard.Set("team", Variant.From((int)parentTurn.team));

        blackboard.Remove("override_target_coords");
        blackboard.Remove("chosen_target_coords");
        
        blackboard.Set("has_scanned", Variant.From(false)); 
    }
}