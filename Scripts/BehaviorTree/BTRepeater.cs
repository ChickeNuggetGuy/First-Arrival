using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Ticks the child a fixed number of times (across turns if
/// the child returns RUNNING). Useful for multi-attack turns.
/// Set Repeats to 0 for infinite repeats (until failure).
/// </summary>
[GlobalClass]
public partial class BTRepeater : BTDecorator
{
    [Export] public int Repeats { get; set; } = 1;
    [Export] public bool AbortOnFailure { get; set; } = true;

    private int _count;

    public override BTStatus Tick(double delta)
    {
        bool infinite = Repeats <= 0;

        while (infinite || _count < Repeats)
        {
            var status = Child.Tick(delta);

            if (status == BTStatus.Running)
                return BTStatus.Running;

            if (status == BTStatus.Failure && AbortOnFailure)
            {
                _count = 0;
                return BTStatus.Failure;
            }

            _count++;
        }

        _count = 0;
        return BTStatus.Success;
    }

    public override void Abort()
    {
        _count = 0;
        base.Abort();
    }
}
