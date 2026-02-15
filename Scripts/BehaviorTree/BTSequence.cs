using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Ticks children left-to-right. Fails immediately if any
/// child fails. Succeeds when ALL children succeed.
/// Resumes from a RUNNING child on subsequent ticks.
/// </summary>
[GlobalClass]
public partial class BTSequence : BTComposite
{
    public override BTStatus Tick(double delta)
    {
        int start = RunningChild >= 0 ? RunningChild : 0;

        for (int i = start; i < Children.Count; i++)
        {
            var status = Children[i].Tick(delta);

            switch (status)
            {
                case BTStatus.Failure:
                    RunningChild = -1;
                    return BTStatus.Failure;

                case BTStatus.Running:
                    RunningChild = i;
                    return BTStatus.Running;

                case BTStatus.Success:
                    continue;
            }
        }

        RunningChild = -1;
        return BTStatus.Success;
    }
}
