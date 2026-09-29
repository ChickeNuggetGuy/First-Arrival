using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;

public enum EventExecutionResult
{
	Completed,
	Blocked,
	Failed,
	Cancelled
}

public enum EventTriggerMode
{
	Automatic,
	Manual
}

[GlobalClass]
public partial class Event : Resource
{
	[Export] public string EventId { get; private set; } = string.Empty;
	[Export] public string EventName { get; private set; } = string.Empty;
	[Export(PropertyHint.MultilineText)]
	public string EventDescription { get; private set; } = string.Empty;
	[Export] public EventTriggerMode TriggerMode { get; private set; }
	[Export] public Array<EventCondition> Conditions { get; private set; } = new();
	[Export] public Array<Event> SubEvents { get; private set; } = new();

	public bool CanAutomaticallyTrigger() =>
		TriggerMode == EventTriggerMode.Automatic && CheckTriggerConditions();

	public bool CheckTriggerConditions() => CheckConditions(Conditions);

	protected bool CheckConditions(Array<EventCondition> conditions)
	{
		foreach (EventCondition condition in conditions)
		{
			if (condition == null)
			{
				GD.PushError($"Event '{EventId}' contains a null condition.");
				return false;
			}
			if (!condition.Check()) return false;
		}
		return true;
	}

	public Task<EventExecutionResult> ExecuteCall(
		bool ignoreConditions = false,
		EventExecutionContext context = null) =>
		ExecuteCall(new HashSet<Event>(), ignoreConditions, context);

	private async Task<EventExecutionResult> ExecuteCall(
		HashSet<Event> activeChain,
		bool ignoreConditions,
		EventExecutionContext context)
	{
		if (context?.CancellationToken.IsCancellationRequested == true)
			return EventExecutionResult.Cancelled;
		if (!activeChain.Add(this))
		{
			GD.PushError($"Event '{EventId}' contains a cyclic sub-event reference.");
			return EventExecutionResult.Failed;
		}
		try
		{
			if (context?.IsCompleted?.Invoke(EventId) == true)
				return EventExecutionResult.Completed;
			if (!ignoreConditions && !CheckTriggerConditions())
				return EventExecutionResult.Blocked;

			context?.Started?.Invoke(EventId);
			EventExecutionResult result = await Execute(context);
			if (context?.CancellationToken.IsCancellationRequested == true)
				return EventExecutionResult.Cancelled;
			if (result != EventExecutionResult.Completed) return result;

			foreach (Event subEvent in SubEvents)
			{
				if (subEvent == null)
				{
					GD.PushError($"Event '{EventId}' contains a null sub-event.");
					return EventExecutionResult.Failed;
				}
				result = await subEvent.ExecuteCall(activeChain, false, context);
				if (result != EventExecutionResult.Completed) return result;
			}

			await ExecuteComplete();
			if (context?.CancellationToken.IsCancellationRequested == true)
				return EventExecutionResult.Cancelled;
			context?.Completed?.Invoke(EventId);
			return EventExecutionResult.Completed;
		}
		finally
		{
			activeChain.Remove(this);
		}
	}

	protected virtual Task<EventExecutionResult> Execute(EventExecutionContext context) =>
		Task.FromResult(EventExecutionResult.Completed);

	protected virtual Task ExecuteComplete() => Task.CompletedTask;
}
