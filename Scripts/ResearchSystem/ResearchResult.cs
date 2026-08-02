using Godot;

[GlobalClass]
public abstract partial class ResearchResult : Resource
{
	public bool TryApply(
		GlobeTeamHolder teamHolder,
		ResearchProject completedProject)
	{
		if (teamHolder == null)
		{
			GD.PushError("A research result cannot be applied without a team.");
			return false;
		}

		try
		{
			OnApply(teamHolder, completedProject);
			return true;
		}
		catch (System.Exception exception)
		{
			string projectId = completedProject?.GetStableId() ?? "unknown";
			GD.PushError(
				$"Failed to apply {GetType().Name} for research '{projectId}': " +
				$"{exception.Message}");
			return false;
		}
	}

	protected abstract void OnApply(
		GlobeTeamHolder teamHolder,
		ResearchProject completedProject);
}
