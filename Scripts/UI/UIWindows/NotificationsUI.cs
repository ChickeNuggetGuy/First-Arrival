using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class NotificationsUI : UIWindow
{
	public static NotificationsUI Instance;
	[Export] private VBoxContainer notificationsHolder;
	[Export(PropertyHint.Range, "0.1,60,0.1,or_greater")]
	private float notificationDuration = 8.0f;
	private readonly List<NotificationElement> currentNotifications = new();
	private GlobeMissionManager missionManager;
	private GlobeTeamManager teamManager;

	public override void _Ready()
	{
		base._Ready();
		if (!TryClaimInstance()) return;
		// Manager signals can fire during the game's execute phase, before
		// UIManager reaches this window's SetupCall.
		ConnectGameplaySignals();
	}

	protected override Task _Setup()
	{
		if (!TryClaimInstance())
			return Task.CompletedTask;

		ConnectGameplaySignals();
		return Task.CompletedTask;
	}

	private bool TryClaimInstance()
	{
		if (Instance != null && !GodotObject.IsInstanceValid(Instance))
			Instance = null;

		if (Instance != null && Instance != this)
		{
			GD.PrintErr("Instance already set");
			QueueFree();
			return false;
		}

		Instance = this;
		return true;
	}

	protected override Task DrawUI()
	{
		return Task.CompletedTask;
	}


	public void AddNotification(NotificationElement notification)
	{
		if (notification == null || notificationsHolder == null) return;
		currentNotifications.Add(notification);
		notificationsHolder.AddChild(notification);
		_ = notification.SetupCall();
	}

	public void RemoveNotification(NotificationElement notification)
	{
		if (currentNotifications.Contains(notification))
		{
			currentNotifications.Remove(notification);
		}
	}

	private void ConnectGameplaySignals()
	{
		DisconnectGameplaySignals();

		missionManager = GlobeMissionManager.Instance;
		if (missionManager != null)
			missionManager.MissionSpawned += OnMissionSpawned;

		teamManager = GlobeTeamManager.Instance;
		if (teamManager == null) return;

		teamManager.CraftDetected += OnCraftDetected;
		teamManager.CraftArrived += OnCraftArrived;
		teamManager.BaseDetected += OnBaseDetected;
		teamManager.FacilityConstructionCompleted +=
			OnFacilityConstructionCompleted;
		teamManager.ResearchProjectCompleted += OnResearchProjectCompleted;
	}

	private void DisconnectGameplaySignals()
	{
		if (missionManager != null && GodotObject.IsInstanceValid(missionManager))
			missionManager.MissionSpawned -= OnMissionSpawned;

		if (teamManager != null && GodotObject.IsInstanceValid(teamManager))
		{
			teamManager.CraftDetected -= OnCraftDetected;
			teamManager.CraftArrived -= OnCraftArrived;
			teamManager.BaseDetected -= OnBaseDetected;
			teamManager.FacilityConstructionCompleted -=
				OnFacilityConstructionCompleted;
			teamManager.ResearchProjectCompleted -= OnResearchProjectCompleted;
		}

		missionManager = null;
		teamManager = null;
	}

	private void OnMissionSpawned(MissionBase mission)
	{
		if (mission == null || mission.cellIndex < 0) return;

		string missionName = mission.MissionType.ToString();
		if (missionManager?.GetActiveMissions().TryGetValue(
			mission.cellIndex,
			out MissionCellDefinition definition) == true &&
			!string.IsNullOrWhiteSpace(definition.definitionName))
		{
			missionName = definition.definitionName;
		}

		AddNotification(new HexCellNotification(
			$"New mission: {missionName}",
			GetNotificationDuration(),
			mission.cellIndex));
	}

	private void OnCraftDetected(Craft craft, int detectingTeam, int cellIndex)
	{
		if (!IsViewingTeam(detectingTeam) || craft == null || cellIndex < 0)
			return;

		AddNotification(new HexCellNotification(
			$"Enemy craft discovered: {GetCraftDisplayName(craft)}",
			GetNotificationDuration(),
			cellIndex));
	}

	private void OnCraftArrived(Craft craft, int craftTeam, int cellIndex)
	{
		if (!IsViewingTeam(craftTeam) || craft == null || cellIndex < 0)
			return;

		AddNotification(new HexCellNotification(
			$"{GetCraftDisplayName(craft)} reached its destination",
			GetNotificationDuration(),
			cellIndex));
	}

	private void OnBaseDetected(
		int detectingTeam,
		int owningTeam,
		int cellIndex,
		string baseName)
	{
		if (!IsViewingTeam(detectingTeam) || detectingTeam == owningTeam ||
			cellIndex < 0)
		{
			return;
		}

		string displayName = string.IsNullOrWhiteSpace(baseName)
			? "Enemy base"
			: baseName;
		AddNotification(new HexCellNotification(
			$"Enemy base discovered: {displayName}",
			GetNotificationDuration(),
			cellIndex));
	}

	private void OnFacilityConstructionCompleted(
		string facilityName,
		string baseName,
		int baseCellIndex,
		int owningTeam)
	{
		if (!IsViewingTeam(owningTeam) || baseCellIndex < 0) return;

		AddNotification(new BaseNotification(
			$"{facilityName} construction completed at {baseName}",
			GetNotificationDuration(),
			baseCellIndex,
			OpenBase));
	}

	private void OnResearchProjectCompleted(
		GlobeTeamHolder holder,
		string projectId)
	{
		if (holder == null || !IsViewingTeam((int)holder.Team)) return;
		ResearchProject project = holder.ResearchDatabase?.GetProject(projectId);
		string projectName = !string.IsNullOrWhiteSpace(project?.DisplayName)
			? project.DisplayName
			: projectId;

		AddNotification(new ResearchNotification(
			$"Research completed: {projectName}",
			GetNotificationDuration(),
			OpenResearchWindow));
	}

	private void OpenResearchWindow()
	{
		GlobeUI globeUi = GetGlobeUi();
		if (globeUi == null)
		{
			GD.PushError("Cannot open research from notification because GlobeUI was not found.");
			return;
		}

		globeUi.OpenResearchWindow();
	}

	private void OpenBase(int baseCellIndex)
	{
		GlobeUI globeUi = GetGlobeUi();
		if (globeUi == null)
		{
			GD.PushError("Cannot open base from notification because GlobeUI was not found.");
			return;
		}

		_ = globeUi.OpenBase(baseCellIndex);
	}

	private GlobeUI GetGlobeUi() =>
		GetParent()?.GetNodeOrNull<GlobeUI>("GlobeUI");

	private bool IsViewingTeam(int team) =>
		teamManager != null && (Enums.UnitTeam)team == teamManager.ViewingTeam;

	private float GetNotificationDuration() =>
		Mathf.Max(0.1f, notificationDuration);

	private static string GetCraftDisplayName(Craft craft)
	{
		string customName = craft.GetName().ToString();
		return !string.IsNullOrWhiteSpace(customName)
			? customName
			: craft.ItemName ?? "Craft";
	}


	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, KeyLabel: Key.N }) return;
		
		HexCellData? cellData = GlobeHexGridManager.Instance.GetRandomCell(false);
		if (cellData == null) return;
		
		HexCellNotification testNotification = new HexCellNotification($"Test {currentNotifications.Count + 1}",2.5f,
			cellData.Value.Index);
		AddNotification(testNotification);
		base._UnhandledInput(@event);
	}

	public override void _ExitTree()
	{
		DisconnectGameplaySignals();
		if (Instance == this)
			Instance = null;
		base._ExitTree();
	}
}
