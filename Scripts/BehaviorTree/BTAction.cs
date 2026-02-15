using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Generic action leaf. Subclass this for concrete actions
/// like "Attack", "Move", "UseItem", etc.
/// </summary>
[GlobalClass]
public abstract partial class BTAction : BTNode
{
    protected bool IsFirstTick { get; private set; } = true;

    /// <summary>
    /// Called on the first tick of this action (when it wasn't
    /// RUNNING last turn). Use for setup.
    /// </summary>
    protected virtual void OnEnter() { }

    /// <summary>
    /// Called when the action finishes (Success or Failure) or
    /// is aborted.
    /// </summary>
    protected virtual void OnExit() { }

    /// <summary>
    /// The actual work. Return Success, Failure, or Running.
    /// </summary>
    protected abstract BTStatus OnTick(double delta);

    public sealed override BTStatus Tick(double delta)
    {
        if (IsFirstTick)
        {
            OnEnter();
            IsFirstTick = false;
        }

        var status = OnTick(delta);

        if (status != BTStatus.Running)
        {
            OnExit();
            IsFirstTick = true;
        }

        return status;
    }

    public override void Abort()
    {
        if (!IsFirstTick)
        {
            OnExit();
            IsFirstTick = true;
        }
    }
}
