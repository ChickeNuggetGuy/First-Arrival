using Godot;
using BehaviorTree.Core;
using FirstArrival.Scripts.Utility;

namespace BehaviorTree.Integration;

public enum ComparisonOp
{
    LessThan,
    LessOrEqual,
    GreaterThan,
    GreaterOrEqual,
    Equal,
}

/// <summary>
/// Checks a GridObject's stat against a threshold.
/// Can compare raw value or percentage of max.
/// </summary>
[GlobalClass]
public partial class BTStatCheck : BTCondition
{
    [Export] public Enums.Stat Stat { get; set; }
    [Export] public ComparisonOp Comparison { get; set; } = ComparisonOp.LessThan;
    [Export] public float Threshold { get; set; } = 0.3f;

    /// <summary>
    /// If true, compares (current / max) as a 0-1 ratio.
    /// If false, compares the raw current value.
    /// </summary>
    [Export] public bool UsePercentage { get; set; } = true;

    protected override bool Check()
    {
	    var gridObject = Tree.ParentGridObject;
        if (gridObject == null) return false;

        if (!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(
                out var statHolder))
            return false;

        if (!statHolder.TryGetStat(Stat, out var stat))
            return false;

        float value = UsePercentage
            ? stat.CurrentValue / (float)stat.MinMaxValue.max
            : stat.CurrentValue;

        return Comparison switch
        {
            ComparisonOp.LessThan => value < Threshold,
            ComparisonOp.LessOrEqual => value <= Threshold,
            ComparisonOp.GreaterThan => value > Threshold,
            ComparisonOp.GreaterOrEqual => value >= Threshold,
            ComparisonOp.Equal => Mathf.IsEqualApprox(value, Threshold),
            _ => false,
        };
    }
}
