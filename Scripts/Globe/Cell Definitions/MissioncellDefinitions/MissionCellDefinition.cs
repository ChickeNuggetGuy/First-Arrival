using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;
using System;

public partial class MissionCellDefinition : HexCellDefinition
{
	public MissionBase mission;

	public Node3D missionVisual = null;
	
	public Enums.MissionStatus missionStatus = Enums.MissionStatus.None;

	[Export] public int timeoutTime = 12;
	public int timeLeft {get; private set;}
	private bool _isTrackingTimeout;
	private bool _hasResolved;
	public MissionScoreBreakdown BattleScore { get; private set; }
	public long RecoverySaleProceeds { get; private set; }
	public bool HasBattleResult => BattleScore != null;

	[Export] public Dictionary<Enums.MissionStatus, int> scoreChange = new()
	{
		{ Enums.MissionStatus.None, 0 },
		{ Enums.MissionStatus.Failed, -250 },
		{ Enums.MissionStatus.Successful, 325 },
		{ Enums.MissionStatus.Timeout, -200 },
		{ Enums.MissionStatus.Aborted, 0 }
	};

	public Craft onRouteCraft; 
	public int alienOperationId { get; private set; } = -1;

	public MissionCellDefinition(
		int cellIndex,
		string name,
		MissionBase mission,
		Node3D missionVisual = null,
		Enums.MissionStatus missionStatus = Enums.MissionStatus.None,
		Craft craft = null,
		int alienOperationId = -1) : base(cellIndex, name)
	{
		this.mission = mission;
		if (missionVisual != null)
			this.missionVisual = missionVisual;
		this.missionStatus = missionStatus;
		this.alienOperationId = alienOperationId;
		SetOnRouteCraft(craft);
		timeLeft = timeoutTime;
		StartTimeoutTracking();
	}

	private void StartTimeoutTracking()
	{
		if (_isTrackingTimeout || GlobeTimeManager.Instance == null) return;
		GlobeTimeManager.Instance.HourChanged += GlobeTimeManagerOnHourChanged;
		_isTrackingTimeout = true;
	}

	/// <summary>Stops the clock and releases the time-manager event reference.</summary>
	public void StopTimeoutTracking()
	{
		if (!_isTrackingTimeout) return;
		if (GlobeTimeManager.Instance != null)
			GlobeTimeManager.Instance.HourChanged -= GlobeTimeManagerOnHourChanged;
		_isTrackingTimeout = false;
	}

	public void RestoreTimeoutState(int savedTimeoutTime, int savedTimeLeft)
	{
		timeoutTime = Math.Max(0, savedTimeoutTime);
		timeLeft = Math.Clamp(savedTimeLeft, 0, timeoutTime);
	}

	public void SetBattleResult(MissionBattleResult result)
	{
		if (result == null) return;
		BattleScore = result.Score;
		RecoverySaleProceeds = Math.Max(0, result.SaleProceeds);
	}

	public Dictionary<string, Variant> SaveBattleResult()
	{
		if (BattleScore == null) return new Dictionary<string, Variant>();
		return new Dictionary<string, Variant>
		{
			{ "enemiesKilled", BattleScore.EnemiesKilled },
			{ "unitsLost", BattleScore.UnitsLost },
			{ "enemyKillPoints", BattleScore.EnemyKillPoints },
			{ "unitLossPoints", BattleScore.UnitLossPoints },
			{ "outcomePoints", BattleScore.OutcomePoints },
			{ "totalPoints", BattleScore.TotalPoints },
			{ "recoverySaleProceeds", RecoverySaleProceeds }
		};
	}

	public void RestoreBattleResult(Dictionary<string, Variant> data)
	{
		if (data == null ||
		    !data.TryGetValue("battleResult", out Variant resultValue) ||
		    resultValue.VariantType != Variant.Type.Dictionary)
			return;

		var result = resultValue.AsGodotDictionary<string, Variant>();
		if (result.Count == 0) return;
		BattleScore = new MissionScoreBreakdown(
			GetInt(result, "enemiesKilled"),
			GetInt(result, "unitsLost"),
			GetInt(result, "enemyKillPoints"),
			GetInt(result, "unitLossPoints"),
			GetInt(result, "outcomePoints"));
		RecoverySaleProceeds = result.TryGetValue(
			"recoverySaleProceeds",
			out Variant proceeds)
			? Math.Max(0, proceeds.AsInt64())
			: 0;
	}

	private static int GetInt(
		Dictionary<string, Variant> data,
		string key) => data.TryGetValue(key, out Variant value)
		? value.AsInt32()
		: 0;

	private void GlobeTimeManagerOnHourChanged(int hour, int hoursAdvanced)
	{
		if (_hasResolved || missionStatus.HasFlag(Enums.MissionStatus.OnRoute)) return;

		timeLeft = Math.Max(0, timeLeft - hoursAdvanced);
		GD.Print($"Mission Tick time left {timeLeft}");
		if (timeLeft > 0) return;

		_hasResolved = true;
		missionStatus |= Enums.MissionStatus.Timeout;
		StopTimeoutTracking();
		GlobeMissionManager.Instance?.ResolveMission(this);
	}

	public void SetOnRouteCraft(Craft craft)
	{
		
		onRouteCraft = craft;
		
		if (onRouteCraft == null) return;

		// Loaded missions may still reference their dispatched craft after the
		// battle has produced a final result. Do not overwrite that result with
		// OnRoute, or GlobeMissionManager will never resolve the mission/reward.
		Enums.MissionStatus completedStatuses = Enums.MissionStatus.Visited |
			Enums.MissionStatus.Successful |
			Enums.MissionStatus.Failed |
			Enums.MissionStatus.Timeout |
			Enums.MissionStatus.Aborted;
		if ((missionStatus & completedStatuses) == Enums.MissionStatus.None)
			missionStatus |= Enums.MissionStatus.OnRoute;
	}

	public override System.Collections.Generic.Dictionary<string, Callable> GetContextActions()
	{
		var actions = base.GetContextActions();
		MissionCellDefinition targetMission = this;

		actions.Add("Mission Details", Callable.From(() =>
		{
			UIManager.Instance?.GetWindow<MissionDetailsUI>()?.ShowMission(targetMission);
		}));

		return actions;
	}
	
	

	public override Dictionary<string, Variant> Save()
	{
		Dictionary<string, Variant> craftData = new Dictionary<string, Variant>() { };
		if (onRouteCraft != null)
		{
			craftData = onRouteCraft.Save();
		}
		var data = base.Save();
		data.Add("missionData", mission.Save());
		data.Add("missionClass", mission.GetType().Name);
		data.Add("missionStatus", (int)missionStatus);
		data.Add("onRouteCraft", craftData);
		data.Add("timeoutTime", timeoutTime);
		data.Add("timeLeft", timeLeft);
		data.Add("alienOperationId", alienOperationId);
		data.Add("battleResult", SaveBattleResult());
		return data;
	}
}
