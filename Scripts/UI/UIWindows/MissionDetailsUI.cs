using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MissionDetailsUI : UIWindow
{
	[Export] private Label missionNameLabel;
	[Export] private Label missionDescriptionLabel;
	[Export] private Label missionTypeLabel;
	[Export] private Label missionDifficultyLabel;
	[Export] private Button sendCraftButton;

	private MissionBase currentMission;
	private MissionCellDefinition currentMissionDefinition;

	protected override Task _Setup()
	{
		MissionUITheme.Apply(this);
		MissionUITheme.InsetPanelContent(this);
		MissionUITheme.StyleFirstTitle(this);
		if (sendCraftButton != null &&
		    !sendCraftButton.IsConnected(
			    Button.SignalName.Pressed,
			    Callable.From(SendCraftButtonOnPressed)))
		{
			sendCraftButton.Pressed += SendCraftButtonOnPressed;
		}

		return Task.CompletedTask;
	}

	public void ShowMission(MissionCellDefinition missionDefinition)
	{
		if (missionDefinition?.mission == null) return;
		currentMissionDefinition = missionDefinition;
		currentMission = missionDefinition.mission;
		_ = ShowCall();
	}

	protected override Task DrawUI()
	{
		if (currentMission == null) return Task.CompletedTask;

		missionNameLabel.Text = currentMission.missionName;
		missionDescriptionLabel.Text = currentMission.missionDescription;
		missionTypeLabel.Text = currentMission.MissionType.ToString();
		missionDifficultyLabel.Text = currentMission.missionDifficulty.ToString();
		if (sendCraftButton != null)
		{
			bool canRespondLocally = currentMissionDefinition?.AllowsLocalResponse == true &&
			                         !currentMissionDefinition.missionStatus.HasFlag(
				                         Enums.MissionStatus.Visited);
			bool hasAvailableCraft = GlobeTeamManager.Instance?.HasAvailableCraft(
				Enums.UnitTeam.Player,
				requireDeployableUnits: true) == true;
			bool canSendCraft = !canRespondLocally && hasAvailableCraft &&
			                    currentMissionDefinition?.onRouteCraft == null;
			sendCraftButton.Visible = canRespondLocally || hasAvailableCraft;
			sendCraftButton.Disabled = !canRespondLocally && !canSendCraft;
			sendCraftButton.Text = canRespondLocally
				? "Respond with Local Police"
				: canSendCraft
					? "Send Craft"
					: "Craft En Route";
		}

		return Task.CompletedTask;
	}

	private void SendCraftButtonOnPressed()
	{
		if (currentMissionDefinition == null ||
		    currentMissionDefinition.onRouteCraft != null) return;
		if (currentMissionDefinition.AllowsLocalResponse)
		{
			_ = GlobeMissionManager.Instance?.LoadLocalResponseMissionScene(
				currentMissionDefinition);
			_ = HideCall();
			return;
		}

		UIManager.Instance?.GetWindow<SelectCraftUI>()?.ShowForDestination(
			currentMissionDefinition.cellIndex);
		_ = HideCall();
	}
}
