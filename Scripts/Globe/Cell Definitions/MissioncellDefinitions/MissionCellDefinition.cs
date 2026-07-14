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

	[Export] public Dictionary<Enums.MissionStatus, int> scoreChange = new()
	{
		{ Enums.MissionStatus.None, 0 },
		{ Enums.MissionStatus.Failed, -250 },
		{ Enums.MissionStatus.Successful, 325 },
		{Enums.MissionStatus.Timeout, -200}
	};

	public Craft onRouteCraft; 

	public MissionCellDefinition(
		int cellIndex,
		string name,
		MissionBase mission,
		Node3D missionVisual = null,
		Enums.MissionStatus missionStatus = Enums.MissionStatus.None,
		Craft craft = null) : base(cellIndex, name)
	{
		this.mission = mission;
		if (missionVisual != null)
			this.missionVisual = missionVisual;
		this.missionStatus = missionStatus;
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

	private void GlobeTimeManagerOnHourChanged(int hour)
	{
		if (_hasResolved || missionStatus.HasFlag(Enums.MissionStatus.OnRoute)) return;

		timeLeft = Math.Max(0, timeLeft - 1);
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
		
		if (onRouteCraft != null)
		{
			missionStatus = Enums.MissionStatus.OnRoute;
		}
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
		return data;
	}
}
