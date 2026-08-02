using Godot;
using Godot.Collections;

[GlobalClass]
public partial class ResearchProject : Resource
{
	[Export] public string ProjectId { get; private set; } = string.Empty;
	[Export] public string DisplayName { get; private set; } = "Research Project";
	[Export(PropertyHint.MultilineText)]
	public string Description { get; private set; } = string.Empty;
	[Export] public Texture2D Icon { get; private set; }
	[Export(PropertyHint.Range, "1,100000000,1,or_greater")]
	public int TotalResearchPoints { get; private set; } = 100;
	[Export(PropertyHint.Range, "0,1000,1,or_greater")]
	public int MaxAssignedScientists { get; private set; }
	[Export] public Array<ResearchProject> Prerequisites { get; private set; } = new();
	[Export] public Array<ResearchResult> ResearchResults { get; private set; } = new();

	public string GetStableId() => ProjectId?.Trim() ?? string.Empty;
}
