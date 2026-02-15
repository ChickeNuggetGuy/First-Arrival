using Godot;

namespace BehaviorTree.Core;

[GlobalClass]
public partial class BTAlwaysFail : BTDecorator
{
    public override BTStatus Tick(double delta)
    {
        var status = Child.Tick(delta);
        return status == BTStatus.Running ? BTStatus.Running : BTStatus.Failure;
    }
}
