using System.Threading.Tasks;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class WaitForConditionsEvent : Event
{
	[Export] public Array<EventCondition> CompletionConditions { get; private set; } = new();

	protected virtual bool CheckCompletionConditions() => CheckConditions(CompletionConditions);

	protected override async Task<EventExecutionResult> Execute(EventExecutionContext context)
	{
		Node owner = context?.Owner;
		if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree())
			return EventExecutionResult.Blocked;
		foreach (EventCondition condition in CompletionConditions)
		{
			if (condition == null) return EventExecutionResult.Failed;
		}

		while (!context.CancellationToken.IsCancellationRequested &&
		       GodotObject.IsInstanceValid(owner) && owner.IsInsideTree())
		{
			if (CheckCompletionConditions()) return EventExecutionResult.Completed;
			await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		return EventExecutionResult.Cancelled;
	}
}
