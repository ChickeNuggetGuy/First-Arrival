using Godot;

[GlobalClass]
public partial class TriggerResearchEventResult : ResearchResult
{
	[Export] public string EventId { get; private set; } = string.Empty;

	protected override void OnApply(
		GlobeTeamHolder teamHolder,
		ResearchProject completedProject)
	{
		teamHolder.QueueResearchEvent(EventId);
	}
}
