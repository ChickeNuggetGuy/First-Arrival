using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Only ticks its child when a blackboard key evaluates to true.
/// Very handy for turn-based: "only consider healing if HP < 30%".
/// </summary>
[GlobalClass]
public partial class BTConditionalGuard : BTDecorator
{
    [Export] public string BlackboardKey { get; set; } = "";

    /// <summary>
    /// If true, the guard checks that the key does NOT exist
    /// (or is false) before proceeding.
    /// </summary>
    [Export] public bool Invert { get; set; } = false;

    public override BTStatus Tick(double delta)
    {
        bool keyIsTrue = Blackboard.Has(BlackboardKey)
            && Blackboard.Get(BlackboardKey).AsBool();

        bool pass = Invert ? !keyIsTrue : keyIsTrue;

        return pass ? Child.Tick(delta) : BTStatus.Failure;
    }
}
