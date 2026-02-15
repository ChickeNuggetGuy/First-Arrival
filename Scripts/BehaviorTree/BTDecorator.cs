using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Base class for single-child wrapper nodes.
/// </summary>
[GlobalClass]
public abstract partial class BTDecorator : BTNode
{
    protected BTNode Child { get; private set; }

    public override void Initialize()
    {
        foreach (var c in GetBTChildren())
        {
            Child = c;
            break;
        }

        if (Child == null)
            GD.PushWarning($"Decorator '{Name}' has no BTNode child.");
    }

    public override void Abort()
    {
        Child?.Abort();
    }
}
