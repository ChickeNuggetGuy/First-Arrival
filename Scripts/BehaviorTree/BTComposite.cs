using Godot;
using System.Collections.Generic;

namespace BehaviorTree.Core;

/// <summary>
/// Base class for nodes with multiple children.
/// </summary>
[GlobalClass]
public abstract partial class BTComposite : BTNode
{
    protected List<BTNode> Children { get; private set; } = new();

    /// <summary>
    /// Index of the child we should resume from when the
    /// composite was RUNNING last turn.
    /// </summary>
    protected int RunningChild { get; set; } = -1;

    public override void Initialize()
    {
        Children.Clear();
        foreach (var child in GetBTChildren())
        {
            Children.Add(child);
        }
    }

    public override void Abort()
    {
        if (RunningChild >= 0 && RunningChild < Children.Count)
        {
            Children[RunningChild].Abort();
        }
        RunningChild = -1;
    }
}
