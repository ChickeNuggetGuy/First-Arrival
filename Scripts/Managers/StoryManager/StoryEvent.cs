using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;

public enum StoryEventExecutionResult
{
	Completed,
	Blocked,
	Failed,
	Cancelled
}

public enum StoryEventTriggerMode
{
	Automatic,
	Manual
}

/// <summary>
/// Immutable, authorable definition of a story event. Runtime progression belongs
/// to StoryManager so shared Resource instances do not become save-state objects.
/// </summary>
[GlobalClass]
public abstract partial class StoryEvent : Resource
{
	[Export] public string EventId { get; private set; } = string.Empty;
	[Export] public string EventName { get; private set; } = string.Empty;
	[Export(PropertyHint.MultilineText)]
	public string EventDescription { get; private set; } = string.Empty;
	[Export] public StoryEventTriggerMode TriggerMode { get; private set; }
	[Export] public int targetTimeScale = 0; 
	[Export(PropertyHint.ResourceType, "EventCondition")] public Array<EventCondition> Conditions { get; private set; } = new();
	[Export(PropertyHint.ResourceType, "StoryEvent")] public Array<StoryEvent> SubEvents { get; private set; } = new();

	public bool CanAutomaticallyTrigger() =>
		TriggerMode == StoryEventTriggerMode.Automatic && CheckTriggerConditions();

	public bool CheckTriggerConditions()
	{
		foreach (EventCondition condition in Conditions)
		{
			if (condition == null)
			{
				GD.PushError($"Story event '{EventId}' contains a null condition.");
				return false;
			}

			if (!condition.Check()) return false;
		}

		return true;
	}

	public Task<StoryEventExecutionResult> ExecuteCall(bool ignoreConditions = false)
	{
		return ExecuteCall(new HashSet<StoryEvent>(), ignoreConditions);
	}

	private async Task<StoryEventExecutionResult> ExecuteCall(
		HashSet<StoryEvent> activeChain,
		bool ignoreConditions)
	{
		if (!ignoreConditions && !CheckTriggerConditions())
			return StoryEventExecutionResult.Blocked;

		if (!activeChain.Add(this))
		{
			GD.PushError($"Story event '{EventId}' contains a cyclic sub-event reference.");
			return StoryEventExecutionResult.Failed;
		}

		try
		{
			GlobeTimeManager.Instance.SetTimeSpeed(targetTimeScale);
			StoryEventExecutionResult result = await Execute();
			if (result != StoryEventExecutionResult.Completed)
				return result;

			foreach (StoryEvent subEvent in SubEvents)
			{
				if (subEvent == null)
				{
					GD.PushError($"Story event '{EventId}' contains a null sub-event.");
					return StoryEventExecutionResult.Failed;
				}

				StoryEventExecutionResult subEventResult =
					await subEvent.ExecuteCall(activeChain, ignoreConditions: false);
				if (subEventResult != StoryEventExecutionResult.Completed)
					return subEventResult;
			}

			await ExecuteComplete();
			return StoryEventExecutionResult.Completed;
		}
		finally
		{
			activeChain.Remove(this);
		}
	}

	protected abstract Task<StoryEventExecutionResult> Execute();

	protected virtual Task ExecuteComplete() => Task.CompletedTask;
}
