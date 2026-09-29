using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;

/// <summary>
/// Displays authored story text through the shared dynamic popup window and waits
/// for the player to dismiss it before completing the story event.
/// </summary>
[GlobalClass]
public partial class PopupStoryEvent : StoryEvent
{
	[Export(PropertyHint.Range, "0,1,1")] public int targetTimeScale = 0;
	[Export] public string PopupTitle { get; private set; } = string.Empty;
	[Export(PropertyHint.MultilineText)]
	public string PopupText { get; private set; } = string.Empty;
	[Export] public string ContinueButtonText { get; private set; } = "Continue";

	[ExportGroup("Mission Location Buttons")]
	[Export] public bool ShowStoryMissionFocusButtons { get; private set; }
	[Export] public string FocusStoryMissionEventId { get; private set; } =
		string.Empty;
	[Export(PropertyHint.Range, "-1,20,0.1")]
	public float MissionFocusZoom { get; private set; } = -1.0f;

	protected override async Task<EventExecutionResult> Execute(EventExecutionContext context)
	{
		PopupWindowUI popupWindow = UIManager.Instance?.GetWindow<PopupWindowUI>();
		if (popupWindow == null)
			return EventExecutionResult.Blocked;

		string title = string.IsNullOrWhiteSpace(PopupTitle)
			? EventName
			: PopupTitle;
		string text = string.IsNullOrWhiteSpace(PopupText)
			? EventDescription
			: PopupText;
		List<PopupAction> actions = BuildMissionFocusActions();

		GlobeTimeManager timeManager = GlobeTimeManager.Instance;
		if (timeManager != null)
			timeManager.SetTimeSpeed(Math.Min(timeManager.GetTimeSpeed(), Math.Clamp(targetTimeScale, 0, 1)));

		PopupResult result = await popupWindow.ShowTextPopupAsync(
			title,
			new[] { text },
			actions,
			ContinueButtonText);
		return result.WasCancelled
			? EventExecutionResult.Cancelled
			: EventExecutionResult.Completed;
	}

	private List<PopupAction> BuildMissionFocusActions()
	{
		var actions = new List<PopupAction>();
		if (!ShowStoryMissionFocusButtons ||
		    string.IsNullOrWhiteSpace(FocusStoryMissionEventId))
			return actions;

		GlobeMissionManager missionManager = GlobeMissionManager.Instance;
		if (missionManager == null) return actions;

		var matchingMissions = missionManager.GetActiveMissions().Values
			.Where(mission =>
				mission != null &&
				mission.StoryEventId == FocusStoryMissionEventId)
			.OrderBy(GetLocationName)
			.ThenBy(mission => mission.cellIndex);

		foreach (MissionCellDefinition mission in matchingMissions)
		{
			int cellIndex = mission.cellIndex;
			string locationName = GetLocationName(mission);
			actions.Add(new PopupAction(
				$"focus_city_{cellIndex}",
				$"View {locationName}",
				async () =>
				{
					OrbitalCamera camera = OrbitalCamera.Instance;
					if (camera == null) return;
					float? zoom = MissionFocusZoom >= 0.0f
						? MissionFocusZoom
						: null;
					await camera.FocusOnCell(cellIndex, zoom);
				}));
		}

		return actions;
	}

	private static string GetLocationName(MissionCellDefinition mission)
	{
		string cityName = GlobeCityManager.Instance?.GetCityName(mission.cellIndex);
		if (!string.IsNullOrWhiteSpace(cityName) && cityName != "City")
			return cityName;
		if (!string.IsNullOrWhiteSpace(mission.definitionName))
			return mission.definitionName;
		return $"Location {mission.cellIndex}";
	}
}
