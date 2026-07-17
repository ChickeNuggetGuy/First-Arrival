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
    [Export] private uint _maxRunTimeMs = 15000;
    
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
	        if( unit.GridPositionData.AnchorCell.fogState == Enums.FogState.Visible)
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

        ulong startedAt = Time.GetTicksMsec();
        BTStatus status = BTStatus.Failure;
        bool timedOut = false;
        bool retryAfterSearch = false;

        do
        {
            do
            {
                status = bt.TickTree();

                if (status == BTStatus.Running)
                {
                    if (Time.GetTicksMsec() - startedAt >= _maxRunTimeMs)
                    {
                        timedOut = true;
                        bt.Abort();
                        GD.PushWarning(
                            $"AI Turn: {unit.Name} timed out after {_maxRunTimeMs} ms."
                        );
                        break;
                    }

                    await unit.ToSignal(unit.GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            } while (status == BTStatus.Running);

            if (timedOut) break;

            // A scan or movement search can reveal an enemy after Combat was
            // evaluated. Restart once so the newly visible target is handled
            // by Combat rather than ending this unit's turn.
            retryAfterSearch = bt.Blackboard.Get<bool>("enemy_revealed_during_action")
                && CanSeeActiveEnemy(unit);

            if (retryAfterSearch)
            {
                bt.Blackboard.Set("has_searched", Variant.From(false));
                bt.Blackboard.Set("enemy_revealed_during_action", Variant.From(false));
                if (unit.GridPositionData?.AnchorCell != null)
                {
                    bt.Blackboard.Set(
                        "start_cell_coords",
                        Variant.From(unit.GridPositionData.AnchorCell.GridCoordinates)
                    );
                }
                GD.Print($"AI Turn: {unit.Name} found an enemy while searching; reevaluating combat.");
            }
        } while (retryAfterSearch);

        GD.Print($"<--- AI Turn: {unit.Name} Finished ({status})");
    }

	private static bool CanSeeActiveEnemy(GridObject unit)
	{
		if (!unit.TryGetGridObjectNode<GridObjectSight>(out var sight))
			return false;

		sight.EnsureUpToDate();

		return sight.SeenGridObjects.Any(gridObject =>
			gridObject != null
			&& gridObject.IsActive
			&& !gridObject.scenery
			&& gridObject.Team != unit.Team
		);
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
        blackboard.Remove("selectedAction");
        blackboard.Set("has_searched", Variant.From(false));
        blackboard.Set("enemy_revealed_during_action", Variant.From(false));
    }
}
