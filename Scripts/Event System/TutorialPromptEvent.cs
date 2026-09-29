using System.Threading.Tasks;
using Godot;

[GlobalClass]
public partial class TutorialPromptEvent : WaitForConditionsEvent
{
	[Export(PropertyHint.Range, "0,3,0.1")]
	public float CompletionPauseSeconds { get; private set; } = 0.4f;

	protected override bool CheckCompletionConditions() =>
		base.CheckCompletionConditions() && TutorialManager.Instance?.IsPromptComplete(this) == true;

	protected override async Task<EventExecutionResult> Execute(EventExecutionContext context)
	{
		TutorialManager manager = TutorialManager.Instance;
		if (manager == null || !manager.BeginPrompt(this)) return EventExecutionResult.Blocked;
		try
		{
			return await base.Execute(context);
		}
		finally
		{
			if (context?.CancellationToken.IsCancellationRequested != true &&
			    GodotObject.IsInstanceValid(manager)) manager.EndPrompt(this);
		}
	}
}
