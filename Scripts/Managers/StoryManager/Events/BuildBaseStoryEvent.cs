using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Globe.Countries;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

public enum BuildBaseStoryTargetMode
{
	SelectedStoryMission,
	CustomHexCellIndex,
	RandomHexCell,
	RandomCityCell
}

public enum StoryMissionOutcomeRequirement
{
	AnyResolvedOutcome,
	SuccessfulMission,
	SelectionOnly
}

/// <summary>
/// Builds a player base as a story action. A selected city mission resolves to
/// a neighboring land cell, keeping the globe's city marker and base marker on
/// separate hexes while preserving their narrative association.
/// </summary>
[GlobalClass]
public partial class BuildBaseStoryEvent : StoryEvent
{
	[ExportGroup("Target")]
	[Export] public BuildBaseStoryTargetMode TargetMode { get; private set; } =
		BuildBaseStoryTargetMode.SelectedStoryMission;
	[Export] public int TargetHexCell { get; private set; } = -1;
	[Export] public bool LimitToCountry { get; private set; }
	[Export] public CountryDefinition TargetCountry { get; private set; }

	[ExportGroup("Selected Story Mission")]
	[Export] public string SourceStoryMissionEventId { get; private set; } =
		string.Empty;
	[Export] public StoryMissionOutcomeRequirement OutcomeRequirement
	{
		get;
		private set;
	} = StoryMissionOutcomeRequirement.AnyResolvedOutcome;
	[Export] public bool PreferSelectedMissionCountry { get; private set; } = true;

	[ExportGroup("Base")]
	[Export] public string BaseName { get; private set; } = "First Base";
	[Export(PropertyHint.Range, "0,1000000000,1000,or_greater")]
	public int ConstructionCost { get; private set; }

	protected override Task<EventExecutionResult> Execute(EventExecutionContext context)
	{
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager == null || GlobeHexGridManager.Instance == null)
			return Task.FromResult(EventExecutionResult.Blocked);

		List<int> candidates;
		if (TargetMode == BuildBaseStoryTargetMode.SelectedStoryMission)
		{
			StoryManager storyManager = StoryManager.Instance;
			if (storyManager == null ||
			    !storyManager.TryGetSelectedStoryMissionCell(
				    SourceStoryMissionEventId,
				    out int selectedCell) ||
			    !MeetsOutcomeRequirement(storyManager))
			{
				return Task.FromResult(EventExecutionResult.Blocked);
			}

			candidates = GetAdjacentBaseCandidates(selectedCell);
		}
		else
		{
			candidates = StoryCellTargetResolver.GetCandidates(
				ToCellTargetMode(TargetMode),
				TargetHexCell,
				LimitToCountry,
				TargetCountry);
			if (TargetMode == BuildBaseStoryTargetMode.RandomCityCell)
				candidates = GetAdjacentBaseCandidates(candidates);
			Shuffle(candidates);
		}

		GlobeMissionManager missionManager = GlobeMissionManager.Instance;
		if (missionManager != null)
			candidates.RemoveAll(missionManager.HasMissionAt);
		GlobeTeamHolder playerTeam = teamManager.GetTeamData(Enums.UnitTeam.Player);
		// Fixed/selected targets can recognize a base built by an earlier retry.
		// Random targets must not treat an unrelated existing base as completion.
		if (TargetMode is BuildBaseStoryTargetMode.SelectedStoryMission or
		    BuildBaseStoryTargetMode.CustomHexCellIndex)
		{
			foreach (int candidate in candidates)
			{
				if (playerTeam?.TryGetBaseAtIndex(
					    candidate,
					    out TeamBaseCellDefinition _) == true)
				{
					return Task.FromResult(EventExecutionResult.Completed);
				}
			}
		}

		foreach (int candidate in candidates)
		{
			HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromIndex(
				candidate,
				excludeWater: true);
			if (cell.HasValue && teamManager.TryBuildStoryBase(
				    cell.Value,
				    BaseName,
				    ConstructionCost))
			{
				return Task.FromResult(EventExecutionResult.Completed);
			}
		}

		return Task.FromResult(EventExecutionResult.Blocked);
	}

	private List<int> GetAdjacentBaseCandidates(int selectedCellIndex)
	{
		GlobeHexGridManager gridManager = GlobeHexGridManager.Instance;
		HexCellData? selectedCell = gridManager?.GetCellFromIndex(selectedCellIndex);
		if (!selectedCell.HasValue)
			return new List<int>();

		List<HexCellData> adjacentCells = gridManager.GetCellsInStepRange(
			selectedCell.Value,
			steps: 1,
			excludeWater: true);
		adjacentCells.RemoveAll(cell => cell.Index == selectedCellIndex);

		uint selectedCountryKey = gridManager.GetCountryKeyForIndex(
			selectedCellIndex);
		if (PreferSelectedMissionCountry && selectedCountryKey != 0)
		{
			List<HexCellData> sameCountryCells = adjacentCells.FindAll(cell =>
				gridManager.GetCountryKeyForIndex(cell.Index) == selectedCountryKey);
			List<HexCellData> fallbackCells = adjacentCells.FindAll(cell =>
				gridManager.GetCountryKeyForIndex(cell.Index) != selectedCountryKey);
			Shuffle(sameCountryCells);
			Shuffle(fallbackCells);
			adjacentCells = sameCountryCells;
			adjacentCells.AddRange(fallbackCells);
		}

		var candidates = new List<int>(adjacentCells.Count);
		if (!PreferSelectedMissionCountry || selectedCountryKey == 0)
			Shuffle(adjacentCells);
		foreach (HexCellData cell in adjacentCells)
			candidates.Add(cell.Index);
		return candidates;
	}

	private static void Shuffle(List<HexCellData> values)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int swapIndex = GD.RandRange(0, index);
			(values[index], values[swapIndex]) =
				(values[swapIndex], values[index]);
		}
	}

	private List<int> GetAdjacentBaseCandidates(List<int> cityCellIndices)
	{
		var uniqueCandidates = new HashSet<int>();
		foreach (int cityCellIndex in cityCellIndices)
		{
			foreach (int candidate in GetAdjacentBaseCandidates(cityCellIndex))
				uniqueCandidates.Add(candidate);
		}
		return new List<int>(uniqueCandidates);
	}

	private bool MeetsOutcomeRequirement(StoryManager storyManager)
	{
		if (OutcomeRequirement == StoryMissionOutcomeRequirement.SelectionOnly)
			return true;

		if (!storyManager.TryGetSelectedStoryMissionOutcome(
			    SourceStoryMissionEventId,
			    out Enums.MissionStatus outcome))
			return false;

		return OutcomeRequirement != StoryMissionOutcomeRequirement.SuccessfulMission ||
		       outcome == Enums.MissionStatus.Successful;
	}

	private static StoryCellTargetMode ToCellTargetMode(
		BuildBaseStoryTargetMode targetMode)
	{
		return targetMode switch
		{
			BuildBaseStoryTargetMode.RandomHexCell =>
				StoryCellTargetMode.RandomHexCell,
			BuildBaseStoryTargetMode.RandomCityCell =>
				StoryCellTargetMode.RandomCityCell,
			_ => StoryCellTargetMode.CustomHexCellIndex
		};
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
