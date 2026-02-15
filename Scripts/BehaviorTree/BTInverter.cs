using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Flips Success ↔ Failure. Running passes through.
/// </summary>
[GlobalClass]
public partial class BTInverter : BTDecorator
{
    public override BTStatus Tick(double delta)
    {
        var status = Child.Tick(delta);
        return status switch
        {
            BTStatus.Success => BTStatus.Failure,
            BTStatus.Failure => BTStatus.Success,
            _ => status,
        };
    }
}
