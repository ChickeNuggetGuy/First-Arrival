namespace BehaviorTree.Core;

/// <summary>
/// The three canonical return states for any BT node.
/// RUNNING is rare in pure turn-based but supported for
/// multi-step actions that span several turns.
/// </summary>
public enum BTStatus
{
    Success,
    Failure,
    Running,
}
