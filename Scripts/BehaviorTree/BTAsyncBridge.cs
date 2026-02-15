using Godot;
using System.Threading.Tasks;
using BehaviorTree.Core;

namespace BehaviorTree.Integration;

/// <summary>
/// Base class for BT leaves that need to await async operations.
/// Returns Running while the task is in-flight, then
/// Success/Failure once it completes.
/// </summary>
[GlobalClass]
public abstract partial class BTAsyncBridge : BTAction
{
    private Task<bool> _pendingTask;
    private bool _taskStarted;
    private bool? _taskResult;

    protected override void OnEnter()
    {
        _pendingTask = null;
        _taskStarted = false;
        _taskResult = null;
    }

    protected override void OnExit()
    {
        _pendingTask = null;
        _taskStarted = false;
        _taskResult = null;
    }

    protected override BTStatus OnTick(double delta)
    {
        // First tick: start the async work
        if (!_taskStarted)
        {
            _taskStarted = true;
            _pendingTask = ExecuteAsync();

            // If it completed synchronously (common for
            // validation failures), return immediately
            if (_pendingTask.IsCompleted)
            {
                return ResolveCompleted(_pendingTask);
            }

            // Wire up continuation for when it finishes
            _pendingTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    GD.PushError(
                        $"BTAsyncBridge '{Name}': {t.Exception?.InnerException?.Message}"
                    );
                    _taskResult = false;
                }
                else
                {
                    _taskResult = t.Result;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

            return BTStatus.Running;
        }

        // Subsequent ticks: check if done
        if (_taskResult.HasValue)
        {
            return _taskResult.Value ? BTStatus.Success : BTStatus.Failure;
        }

        if (_pendingTask is { IsCompleted: true })
        {
            return ResolveCompleted(_pendingTask);
        }

        return BTStatus.Running;
    }

    private BTStatus ResolveCompleted(Task<bool> task)
    {
        if (task.IsFaulted)
        {
            GD.PushError(
                $"BTAsyncBridge '{Name}': {task.Exception?.InnerException?.Message}"
            );
            return BTStatus.Failure;
        }

        return task.Result ? BTStatus.Success : BTStatus.Failure;
    }

    /// <summary>
    /// Override this. Return true for Success, false for Failure.
    /// You can freely await inside.
    /// </summary>
    protected abstract Task<bool> ExecuteAsync();
}
