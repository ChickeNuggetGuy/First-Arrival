using Godot;

public enum EventSource
{
	Story,
	Tutorial
}

public enum EventStatus
{
	Started,
	Completed
}

[GlobalClass]
public partial class EventStatusCondition : EventCondition
{
	[Export] public string EventId { get; private set; } = string.Empty;
	[Export] public EventSource Source { get; private set; }
	[Export] public EventStatus RequiredStatus { get; private set; } = EventStatus.Completed;

	public override bool Check() => Source switch
	{
		EventSource.Story => RequiredStatus == EventStatus.Started
			? StoryManager.Instance?.IsEventStarted(EventId) == true
			: StoryManager.Instance?.IsEventCompleted(EventId) == true,
		EventSource.Tutorial => RequiredStatus == EventStatus.Started
			? TutorialManager.Instance?.IsEventStarted(EventId) == true
			: TutorialManager.Instance?.IsEventCompleted(EventId) == true,
		_ => false
	};
}
