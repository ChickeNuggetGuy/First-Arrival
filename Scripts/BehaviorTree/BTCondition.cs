using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// A leaf that checks a condition and returns Success/Failure
/// in a single tick. Never returns Running.
/// Subclass and override Check().
/// </summary>
[GlobalClass]
public abstract partial class BTCondition : BTNode
{
    protected abstract bool Check();

    public sealed override BTStatus Tick(double delta)
    {
        return Check() ? BTStatus.Success : BTStatus.Failure;
    }
}
