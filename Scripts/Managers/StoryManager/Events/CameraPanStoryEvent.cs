using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;

[GlobalClass]
public partial class CameraPanStoryEvent : StoryEvent
{
	[Export] public int TargetHexCellIndex { get; private set; } = -1;

	protected override async Task<EventExecutionResult> Execute(EventExecutionContext context)
	{
		OrbitalCamera camera = OrbitalCamera.Instance;
		GlobeHexGridManager gridManager = GlobeHexGridManager.Instance;
		if (camera == null || gridManager == null)
			return EventExecutionResult.Blocked;

		if (TargetHexCellIndex < 0 ||
		    !gridManager.GetCellFromIndex(TargetHexCellIndex).HasValue)
		{
			GD.PushError(
				$"Story event '{EventId}' has invalid target cell {TargetHexCellIndex}.");
			return EventExecutionResult.Failed;
		}

		await camera.FocusOnCell(TargetHexCellIndex);
		return EventExecutionResult.Completed;
	}
}
