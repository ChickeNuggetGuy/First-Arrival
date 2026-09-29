using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Globe.Countries;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class MissionStoryEvent : StoryEvent
{
	[ExportGroup("Target")]
	[Export] public StoryCellTargetMode TargetMode { get; private set; } =
		StoryCellTargetMode.CustomHexCellIndex;
	[Export] public int TargetHexCell { get; private set; } = -1;
	[Export] public bool LimitToCountry { get; private set; }
	[Export] public CountryDefinition TargetCountry { get; private set; }
	[Export(PropertyHint.Range, "1,8,1")]
	public int MissionCount { get; private set; } = 1;

	[ExportGroup("Mission")]
	[Export] public Enums.MissionType MissionType { get; private set; }
	[Export] public int BaseDifficulty { get; private set; } = 1;

	[ExportGroup("Opening Local Response")]
	[Export] public bool AllowLocalResponse { get; private set; }
	[Export] public bool ExclusiveLocalResponseChoice { get; private set; } = true;
	[Export(PropertyHint.Range, "1,12,1")]
	public int LocalResponderCount { get; private set; } = 3;

	protected override Task<EventExecutionResult> Execute(EventExecutionContext context)
	{
		GlobeMissionManager missionManager = GlobeMissionManager.Instance;
		if (GlobeHexGridManager.Instance == null || missionManager == null)
			return Task.FromResult(EventExecutionResult.Blocked);

		int requestedCount = Mathf.Max(1, MissionCount);
		if (TargetMode == StoryCellTargetMode.CustomHexCellIndex &&
		    TargetHexCell >= 0 && requestedCount > 1)
		{
			GD.PushWarning(
				$"Story mission event '{EventId}' cannot create multiple missions " +
				"at one custom cell index.");
			return Task.FromResult(EventExecutionResult.Blocked);
		}

		List<int> candidates = StoryCellTargetResolver.GetCandidates(
			TargetMode,
			TargetHexCell,
			LimitToCountry,
			TargetCountry);
		candidates.RemoveAll(missionManager.HasMissionAt);
		if (candidates.Count < requestedCount ||
		    !missionManager.CanCreateStoryMissions(requestedCount))
			return Task.FromResult(EventExecutionResult.Blocked);

		Shuffle(candidates);
		var createdMissions = new List<MissionCellDefinition>();
		for (int index = 0; index < requestedCount; index++)
		{
			int targetHexCell = candidates[index];
			string missionName = GetMissionName(targetHexCell);
			if (missionManager.TryCreateStoryMission(
				targetHexCell,
				MissionType,
				BaseDifficulty,
				missionName,
				EventId,
				AllowLocalResponse,
				ExclusiveLocalResponseChoice,
				LocalResponderCount,
				out MissionCellDefinition missionDefinition))
			{
				if (!string.IsNullOrWhiteSpace(EventDescription))
					missionDefinition.mission.missionDescription = EventDescription;
				createdMissions.Add(missionDefinition);
				continue;
			}

			// Keep retries idempotent if an unexpected failure occurs after only
			// part of a group was created.
			foreach (MissionCellDefinition createdMission in createdMissions)
				missionManager.RemoveMissionDefinition(createdMission);
			return Task.FromResult(EventExecutionResult.Blocked);
		}

		return Task.FromResult(EventExecutionResult.Completed);
	}

	private string GetMissionName(int cellIndex)
	{
		string baseName = string.IsNullOrWhiteSpace(EventName)
			? "Story Mission"
			: EventName;
		if (TargetMode != StoryCellTargetMode.RandomCityCell)
			return baseName;

		string cityName = GlobeCityManager.Instance?.GetCityName(cellIndex);
		return string.IsNullOrWhiteSpace(cityName) || cityName == "City"
			? baseName
			: $"{baseName}: {cityName}";
	}

	private static void Shuffle(List<int> values)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int swapIndex = GD.RandRange(0, index);
			(values[index], values[swapIndex]) =
				(values[swapIndex], values[index]);
		}
	}
}
