using Godot;
using BehaviorTree.Core;

namespace BehaviorTree.Integration;

/// <summary>
/// Checks whether the grid object can afford and validly
/// execute this action against at least one target.
/// Lighter than BTHasValidTargets when you just need
/// an affordability gate.
/// </summary>
[GlobalClass]
public partial class BTCanAffordAction : BTCondition
{
    [Export] public ActionDefinition ActionDef { get; set; }

    protected override bool Check()
    {
        if (ActionDef == null) return false;

        var gridObject = Blackboard.Get<GridObject>("grid_object");
        if (gridObject == null) return false;

        var startCell = gridObject.GridPositionData.AnchorCell;
        if (startCell == null) return false;

        ActionDef.parentGridObject = gridObject;

        // Use DetermineBestAIAction — it already checks CanTakeAction
        var (cell, score, costs) = ActionDef.DetermineBestAIAction();
        return cell != null;
    }
}
