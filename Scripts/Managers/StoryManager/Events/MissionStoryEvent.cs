using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class MissionStoryEvent : StoryEvent
{
	[Export] public int TargetHexCell { get; private set; } = -1;
	[Export] public Enums.MissionType MissionType { get; private set; }
	[Export] public int BaseDifficulty { get; private set; } = 1;

	protected override Task<StoryEventExecutionResult> Execute()
	{
		GlobeHexGridManager gridManager = GlobeHexGridManager.Instance;
		GlobeMissionManager missionManager = GlobeMissionManager.Instance;
		if (gridManager == null || missionManager == null)
			return Task.FromResult(StoryEventExecutionResult.Blocked);

		int targetHexCell = TargetHexCell;
		if (targetHexCell < 0)
		{
			HexCellData? possibleCell = gridManager.GetRandomCell(true);
			if (!possibleCell.HasValue)
				return Task.FromResult(StoryEventExecutionResult.Blocked);

			targetHexCell = possibleCell.Value.Index;
		}

		string missionName = string.IsNullOrWhiteSpace(EventName)
			? "Story Mission"
			: EventName;
		bool created = missionManager.TryCreateStoryMission(
			targetHexCell,
			MissionType,
			BaseDifficulty,
			missionName);

		return Task.FromResult(created
			? StoryEventExecutionResult.Completed
			: StoryEventExecutionResult.Blocked);
	}
}
