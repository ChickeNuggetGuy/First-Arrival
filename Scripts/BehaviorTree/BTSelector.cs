using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Ticks children left-to-right. Succeeds immediately if any
/// child succeeds. Fails when ALL children fail.
/// </summary>
[GlobalClass]
public partial class BTSelector : BTComposite
{
    public override BTStatus Tick(double delta)
    {
        int start = RunningChild >= 0 ? RunningChild : 0;

        for (int i = start; i < Children.Count; i++)
        {
            var status = Children[i].Tick(delta);

            switch (status)
            {
                case BTStatus.Success:
                    RunningChild = -1;
                    return BTStatus.Success;

                case BTStatus.Running:
                    RunningChild = i;
                    return BTStatus.Running;

                case BTStatus.Failure:
                    continue;
            }
        }

        RunningChild = -1;
        return BTStatus.Failure;
    }
}
