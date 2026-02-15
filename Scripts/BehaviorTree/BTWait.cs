using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Returns RUNNING for a set number of turns, then succeeds.
/// Useful in turn-based for "charge up for N turns" mechanics.
/// </summary>
[GlobalClass]
public partial class BTWait : BTNode
{
    [Export] public int TurnsToWait { get; set; } = 1;

    private int _turnsWaited;

    public override BTStatus Tick(double delta)
    {
        _turnsWaited++;

        if (_turnsWaited >= TurnsToWait)
        {
            _turnsWaited = 0;
            return BTStatus.Success;
        }

        return BTStatus.Running;
    }

    public override void Abort()
    {
        _turnsWaited = 0;
    }
}
