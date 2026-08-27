using Godot;
using Godot.Collections;

public enum StoryTrackMode
{
	/// <summary>Only the first unfinished event is eligible.</summary>
	Linear,
	/// <summary>Every unfinished event is independently eligible.</summary>
	Independent,
	/// <summary>One eligible unfinished event is selected randomly.</summary>
	RandomPool
}

/// <summary>
/// Groups story events that share the same progression rules.
/// </summary>
[GlobalClass]
public partial class StoryTrackDefinition : Resource
{
	[Export] public string TrackId { get; private set; } = string.Empty;
	[Export] public StoryTrackMode Mode { get; private set; }
	[Export(PropertyHint.ResourceType, "StoryEvent")]
	public Array<StoryEvent> Events { get; private set; } = new();
}
