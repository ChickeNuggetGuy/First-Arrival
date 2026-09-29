using System;
using FirstArrival.Scripts.Utility;

public partial class GridObject
{
    // Stable across battle saves, inventory transfers and campaign storage.
    public string UnitId { get; private set; } = Guid.NewGuid().ToString("N");
    public GridObjectCondition Condition { get; private set; }
    public bool IsRestoring { get; private set; }
    public bool CanAct => IsActive && (Condition == null || Condition.State == Enums.UnitCondition.Conscious);

    private void InitializeCondition()
    {
        // Doors/scenery also have health. Only tactical units get consciousness.
        if (scenery || !TryGetGridObjectNode<GridObjectStatHolder>(out var stats) ||
            !stats.TryGetStat(Enums.Stat.Health, out _) ||
            !stats.TryGetStat(Enums.Stat.TimeUnits, out _)) return;

        if (!stats.TryGetStat(Enums.Stat.Stun, out var stun))
        {
            stun = new GridObjectStat(Enums.Stat.Stun, 0, 0, 10000) { Name = "Stun" };
            stats.AddChild(stun);
            stats.RegisterStat(stun);
            gridObjectNodes.Add(stun);
            stun.SetupCall(this);
        }

        if (!TryGetGridObjectNode<GridObjectCondition>(out var condition))
        {
            condition = new GridObjectCondition { Name = "Condition" };
            GridObjectNodeHolder.AddChild(condition);
            gridObjectNodes.Add(condition);
        }
        Condition = condition;
        Condition.SetupCall(this);
    }
}
