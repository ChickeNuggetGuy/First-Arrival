using Godot;
using Godot.Collections;

[GlobalClass]
public partial class StoryDefinition : Resource
{
	[Export] public string StoryId { get; private set; } = string.Empty;

	[Export(PropertyHint.ResourceType, "StoryTrackDefinition")]
	public Array<StoryTrackDefinition> Tracks { get; private set; } = new();
}
