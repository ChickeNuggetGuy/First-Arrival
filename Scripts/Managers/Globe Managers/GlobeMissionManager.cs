using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class GlobeMissionManager : Manager<GlobeMissionManager>
{

    [Export] private PackedScene missionScene;
    [Export] private Node missionContainer;
    [Export] private float missionInterval = 10.0f;
    [Export] private int missionSpawnRangeSteps = 6;
	[Export] private int maxActiveMissions = 8;
	[Export] private bool spawnLegacyRandomMissions = false;

	[ExportGroup("Country Opinion")]
	[Export] private Godot.Collections.Dictionary<Enums.MissionType, float>
		failedMissionOpinionLoss = new()
		{
			{ Enums.MissionType.None, 5.0f },
			{ Enums.MissionType.Eliminate, 8.0f },
			{ Enums.MissionType.Survive, 10.0f },
			{ Enums.MissionType.Objective, 12.0f },
			{ Enums.MissionType.Timed, 6.0f },
			{ Enums.MissionType.CityDefense, 20.0f },
			{ Enums.MissionType.ScoutLanding, 8.0f },
			{ Enums.MissionType.Abduction, 12.0f }
		};

    public int GlobalDifficulty { get; set; } = 1;

    private float _currentMissionTimer;

    private System.Collections.Generic.Dictionary<int, MissionCellDefinition>
        _activeMissions = new();

    #region Signals
    [Signal]
    public delegate void MissionSpawnedEventHandler(MissionBase mission);
    [Signal]
    public delegate void MissionCompletedEventHandler();
    #endregion

    public override string GetManagerName() => "GlobeMissionManager";

    protected override async Task _Setup(bool loadingData)
    {
        if (!loadingData)
        {
            _activeMissions =
                new System.Collections.Generic.Dictionary<
                    int,
                    MissionCellDefinition
                >();
            _currentMissionTimer = missionInterval;
        }
        await Task.CompletedTask;
    }

    protected override async Task _Execute(bool loadingData)
    {
        if (loadingData)
        {
            foreach (var missionDef in _activeMissions.Values.ToArray())
            {
                if (missionDef.missionStatus.HasFlag(Enums.MissionStatus.Visited))
                {
                    ResolveMission(missionDef);
                    continue;
                }

				if (missionDef.timeLeft <= 0)
				{
					ResolveMission(missionDef);
					continue;
				}

                var cell = GlobeHexGridManager.Instance.GetCellFromIndex(
                    missionDef.cellIndex,
                    excludeWater: true
                );
                if (cell.HasValue)
                {
                    SpawnMissionVisual(cell.Value, $"Mission_{missionDef.cellIndex}");
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || !spawnLegacyRandomMissions)
            return;

        _currentMissionTimer -= (float)delta;

        if (_currentMissionTimer <= 0f)
        {
            AttemptSpawnMissionNearPlayer();
            _currentMissionTimer = missionInterval;
        }
    }

    private void AttemptSpawnMissionNearPlayer()
    {
        int unresolvedMissionCount = _activeMissions.Values.Count(mission =>
            !mission.missionStatus.HasFlag(Enums.MissionStatus.Visited)
        );
        if (unresolvedMissionCount >= maxActiveMissions)
            return;

        var gridManager = GlobeHexGridManager.Instance;
        var teamManager = GlobeTeamManager.Instance;

        if (gridManager == null || teamManager == null)
            return;

        var allTeams = teamManager.GetAllTeamData();
        if (!allTeams.ContainsKey(Enums.UnitTeam.Player))
            return;

        var playerTeamData = allTeams[Enums.UnitTeam.Player];
        if (playerTeamData.Bases.Count == 0)
            return;

        HashSet<int> candidateCellIndices = new();
        HashSet<int> occupiedIndices = new();

        foreach (var b in playerTeamData.Bases)
            occupiedIndices.Add(b.cellIndex);
        foreach (var k in _activeMissions.Keys)
            occupiedIndices.Add(k);

        foreach (var baseDef in playerTeamData.Bases)
        {
            var baseCell = gridManager.GetCellFromIndex(baseDef.cellIndex);
            if (baseCell == null)
                continue;

            List<HexCellData> cellsInRange = gridManager.GetCellsInStepRange(
                baseCell.Value,
                missionSpawnRangeSteps,
                excludeWater: true
            );

            foreach (var cell in cellsInRange)
            {
                if (!occupiedIndices.Contains(cell.Index))
                {
                    candidateCellIndices.Add(cell.Index);
                }
            }
        }

        if (candidateCellIndices.Count > 0)
        {
            int[] candidates = candidateCellIndices.ToArray();
            int randomIndex = candidates[GD.RandRange(0, candidates.Length - 1)];

            HexCellData? targetCell = gridManager.GetCellFromIndex(
                randomIndex,
                excludeWater: true
            );

            if (targetCell.HasValue)
            {
                TrySpawnNewMissionCell(targetCell.Value, Enums.MissionType.None);
            }
        }
    }

    public bool TrySpawnNewMissionCell(HexCellData cell, Enums.MissionType missionType)
		=> TryCreateMission(
			cell,
			missionType,
			difficulty: -1,
			alienOperationId: -1,
			missionName: "New Mission");

	/// <summary>
	/// Creates the player-facing mission produced by an alien strategic
	/// operation. The operation id is persisted with the mission so battle scene
	/// transitions can report the eventual outcome back to GlobeAIManager.
	/// </summary>
	public bool TryCreateAlienMission(
		int operationId,
		int targetCellIndex,
		Enums.MissionType missionType,
		int difficulty)
	{
		HexCellData? cell = GlobeHexGridManager.Instance?.GetCellFromIndex(
			targetCellIndex,
			excludeWater: true);
		return cell.HasValue && TryCreateMission(
			cell.Value,
			missionType,
			difficulty,
			operationId,
			$"Alien Attack: {GlobeCityManager.Instance?.GetCityName(targetCellIndex) ?? "City"}");
	}

	/// <summary>
	/// Creates a mission requested by the story system. It is deliberately not
	/// associated with a GlobeAIManager operation, so resolving it cannot alter an
	/// unrelated alien operation that happens to share the same numeric id.
	/// </summary>
	public bool TryCreateStoryMission(
		int targetCellIndex,
		Enums.MissionType missionType,
		int difficulty,
		string missionName)
	{
		HexCellData? cell = GlobeHexGridManager.Instance?.GetCellFromIndex(
			targetCellIndex,
			excludeWater: true);
		return cell.HasValue && TryCreateMission(
			cell.Value,
			missionType,
			difficulty,
			alienOperationId: -1,
			string.IsNullOrWhiteSpace(missionName) ? "Story Mission" : missionName);
	}

	/// <summary>
	/// Creates a landing mission from a local-authority report. This deliberately
	/// does not consult craft detection: countries can reveal the incident while
	/// the UFO itself remains hidden from the player.
	/// </summary>
	public bool TryCreateReportedLandingMission(
		int operationId,
		int targetCellIndex,
		Enums.MissionType missionType,
		int difficulty)
	{
		HexCellData? cell = GlobeHexGridManager.Instance?.GetCellFromIndex(
			targetCellIndex,
			excludeWater: true);
		if (!cell.HasValue) return false;

		string activity = missionType == Enums.MissionType.Abduction
			? "abduction"
			: "scouting";
		string reporter = GlobeHexGridManager.Instance
			?.GetCountryStateForIndex(targetCellIndex)?.CountryName
			?? "Local authorities";

		return TryCreateMission(
			cell.Value,
			missionType,
			difficulty,
			operationId,
			$"{reporter} reports alien {activity}");
	}

	private bool TryCreateMission(
		HexCellData cell,
		Enums.MissionType missionType,
		int difficulty,
		int alienOperationId,
		string missionName)
    {
        if (cell.cellType == Enums.HexGridType.Water)
            return false;

        if (_activeMissions.ContainsKey(cell.Index))
            return false;

        int unresolvedMissionCount = _activeMissions.Values.Count(missionDefinition =>
	        !missionDefinition.missionStatus.HasFlag(Enums.MissionStatus.Visited));
		if (unresolvedMissionCount >= maxActiveMissions)
			return false;

		MissionBase mission = GenerateRandomMission(cell.Index, missionType, "New Mission TEST","Alien Activity sighted", difficulty);
        if (mission == null)
            return false;

        MissionCellDefinition missionCellDefinition = new MissionCellDefinition(
            cell.Index,
			missionName,
			mission,
			null,
			alienOperationId: alienOperationId
        );
        
        _activeMissions.Add(cell.Index, missionCellDefinition);
        SpawnMissionVisual(cell, $"Mission_{cell.Index}");
        
        EmitSignal(SignalName.MissionSpawned, mission);
        return true;
    }

    public MissionBase GenerateRandomMission(int cellIndex, Enums.MissionType missionType = Enums.MissionType.None,
	    string name = "", string description = "",
        int difficulty = -1)
    {
        int enemyCount;

        if (missionType == Enums.MissionType.None)
        {
            Enums.MissionType[] values = Enum.GetValues<Enums.MissionType>()
	            .Where(value => value != Enums.MissionType.None &&
	                            value != Enums.MissionType.CityDefense &&
	                            value != Enums.MissionType.ScoutLanding &&
	                            value != Enums.MissionType.Abduction)
	            .ToArray();
	        int missionIndex = GD.RandRange(0, values.Length - 1);
            missionType = values[missionIndex];
        }

        if (difficulty == -1)
        {
            int minDifficulty = Math.Clamp(GlobalDifficulty - 2, 1, 10);
            int maxDifficulty = Math.Clamp(GlobalDifficulty + 2, 1, 10);
            difficulty = GD.RandRange(minDifficulty, maxDifficulty);
        }

        if (difficulty <= 4)
            enemyCount = GD.RandRange(1, 3);
        else if (difficulty <= 7)
            enemyCount = GD.RandRange(3, 6);
        else
            enemyCount = GD.RandRange(4, 10);

        return new EliminateMission(name, description, missionType, difficulty, enemyCount, cellIndex);
    }

	/// <summary>
	/// Releases any unresolved mission that still references a craft whose route
	/// has been replaced, allowing that mission's timeout to resume.
	/// </summary>
	public void ClearCraftAssignment(Craft craft)
	{
		if (craft == null) return;

		foreach (MissionCellDefinition missionDefinition in _activeMissions.Values)
		{
			if (missionDefinition?.onRouteCraft != craft) continue;

			missionDefinition.SetOnRouteCraft(null);
			missionDefinition.missionStatus &= ~Enums.MissionStatus.OnRoute;
		}
	}
    

    public async Task<bool> LoadMissionScene(MissionCellDefinition missionDefinition)
    {
	    if (missionDefinition?.mission == null ||
	        missionDefinition.onRouteCraft?.HasDeployableUnits != true ||
	        SavesManager.Instance == null ||
	        GameManager.Instance == null)
		    return false;

	    Enums.MissionStatus previousStatus = missionDefinition.missionStatus;
	    var globeState = SavesManager.Instance.GetSceneTransitionState()
		    .Duplicate(true);
	    SavesManager.Instance.SetSessionData("GlobeState", globeState);

	    // Snapshot the craft payload before its globe-scene unit nodes are freed.
	    GameManager.Instance.PrepareBattleLoadout(missionDefinition.onRouteCraft);

	    // Set up the remaining battle parameters. The player count now comes from
	    // the craft snapshot instead of a random value.
	    GameManager.Instance.unitCounts = new Vector2I(
		    GameManager.Instance.unitCounts.X,
		    missionDefinition.mission.EnemySpawnCount
	    );
	    GameManager.Instance.mapSize = new Vector2I(GD.RandRange(3,4), GD.RandRange(3,4));
	    GameManager.Instance.currentMission = missionDefinition;
	    missionDefinition.missionStatus |= Enums.MissionStatus.Visited;

	    // Switch to battle scene WITHOUT saving anything else
	    SavesManager.LoadFromAutosave = false;
	    SavesManager.PendingSaveData = null;
	    SavesManager.PendingSaveName = "";

	    bool changed = false;
	    try
	    {
		    changed = await GameManager.Instance.TryChangeScene(
			    GameManager.GameScene.BattleScene,
			    saveManagerData: false);
	    }
	    catch (Exception exception)
	    {
		    GD.PrintErr(
			    $"The mission battle could not finish loading: {exception.Message}");
	    }

	    if (changed) return true;
	    GameManager.Instance.ClearPendingBattleLoadout();
	    GameManager.Instance.currentMission = null;

	    if (GodotObject.IsInstanceValid(this) && IsInsideTree())
	    {
		    Craft craft = missionDefinition.onRouteCraft;
		    missionDefinition.SetOnRouteCraft(null);
		    missionDefinition.missionStatus = previousStatus & ~(
			    Enums.MissionStatus.Visited |
			    Enums.MissionStatus.OnRoute);
		    craft?.GoToBase();
		    return false;
	    }

	    ResetFailedMissionLaunch(globeState, missionDefinition.cellIndex);
	    SavesManager.PendingSaveData = globeState;
	    SavesManager.LoadFromAutosave = false;
	    try
	    {
		    await GameManager.Instance.ChangeSceneAsync(
			    GameManager.GameScene.GlobeScene,
			    true);
	    }
	    catch (Exception exception)
	    {
		    SavesManager.PendingSaveData = null;
		    GD.PrintErr(
			    $"The globe scene could not be restored: {exception.Message}");
	    }
	    return false;
    }

	private static void ResetFailedMissionLaunch(
		Godot.Collections.Dictionary<string, Variant> root,
		int cellIndex)
	{
		if (root == null ||
		    !root.TryGetValue("managers", out Variant managersValue))
			return;
		var managers = managersValue.AsGodotDictionary<string, Variant>();
		if (!managers.TryGetValue(
			    "GlobeMissionManager",
			    out Variant missionManagerValue))
			return;
		var managerData = missionManagerValue
			.AsGodotDictionary<string, Variant>();
		if (!managerData.TryGetValue(
			    "activeMissions",
			    out Variant missionsValue))
			return;
		var missions = missionsValue.AsGodotDictionary<string, Variant>();
		if (!missions.TryGetValue(cellIndex.ToString(), out Variant missionValue))
			return;

		var missionData = missionValue.AsGodotDictionary<string, Variant>();
		Enums.MissionStatus status = missionData.TryGetValue(
			"missionStatus",
			out Variant statusValue)
			? (Enums.MissionStatus)statusValue.AsInt32()
			: Enums.MissionStatus.None;
		status &= ~(Enums.MissionStatus.Visited | Enums.MissionStatus.OnRoute);
		missionData["missionStatus"] = (int)status;
		missionData["onRouteCraft"] =
			new Godot.Collections.Dictionary<string, Variant>();
		missions[cellIndex.ToString()] = missionData;
		managerData["activeMissions"] = missions;
		managers["GlobeMissionManager"] = managerData;
		root["managers"] = managers;
	}


    private Node3D SpawnMissionVisual(HexCellData cell, string name)
    {
        if (cell.cellType == Enums.HexGridType.Water)
            return null;

        if (missionScene == null)
            return null;

        Node3D missionInstance = missionScene.Instantiate<Node3D>();
        if (missionContainer != null)
            missionContainer.AddChild(missionInstance);
        else
            AddChild(missionInstance);
        
        _activeMissions[cell.Index].missionVisual = missionInstance;
		if (missionInstance is CellDefinitionVisual missionVisual)
			missionVisual.BindDefinition(_activeMissions[cell.Index]);

        missionInstance.GlobalPosition = cell.Center;

        Vector3 surfaceNormal = cell.Center.Normalized();
        Vector3 upDir = Mathf.Abs(surfaceNormal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
        missionInstance.LookAt(cell.Center + surfaceNormal, upDir);
        missionInstance.Name = name;
        return missionInstance;
    }


    public void RemoveMissionDefinition(MissionCellDefinition mission)
    {
	    if (mission == null
	        || !_activeMissions.TryGetValue(mission.cellIndex, out var activeMission)
	        || activeMission != mission)
	        return;

	    mission.StopTimeoutTracking();
	    DestroyMissionVisual(mission);
	    _activeMissions.Remove(mission.cellIndex);
	    EmitSignal(SignalName.MissionCompleted);
    }

    /// <summary>
    /// Completed mission definitions remain in the save as history. When the
    /// globe is rebuilt, apply their result and return the craft that visited
    /// the site instead of restoring a live mission marker.
    /// </summary>
    public void ResolveMission(MissionCellDefinition missionDefinition)
    {
	    if (missionDefinition == null ||
	        !_activeMissions.TryGetValue(
		        missionDefinition.cellIndex,
		        out MissionCellDefinition activeMission) ||
	        activeMission != missionDefinition)
		    return;

        GlobeTeamManager teamManager = GlobeTeamManager.Instance;
        GlobeTeamHolder playerTeam = teamManager?.GetTeamData(Enums.UnitTeam.Player);

		Enums.MissionStatus outcome = GetMissionOutcome(missionDefinition);
		if (missionDefinition.alienOperationId >= 0 && outcome != Enums.MissionStatus.None)
			GlobeAIManager.Instance?.ResolveOperation(
				missionDefinition.alienOperationId,
				outcome);
		if (playerTeam != null && outcome != Enums.MissionStatus.None)
			ApplyMissionRewards(playerTeam, missionDefinition, outcome);

        if (outcome == Enums.MissionStatus.Failed)
	        ApplyFailedMissionOpinionPenalty(missionDefinition);

		Craft craft = playerTeam == null
			? null
			: FindMissionCraft(playerTeam, missionDefinition.onRouteCraft);
        
		if (craft != null && craft.CurrentCellIndex != craft.HomeBaseIndex)
		{
			TeamBaseCellDefinition homeBase = craft.GetBaseCellDefinition();
			if (homeBase != null)
			{
				_ = homeBase.SendCraft(
					craft.CurrentCellIndex,
					craft.HomeBaseIndex,
					craft,
					teamManager);
			}
		}

		RemoveMissionDefinition(missionDefinition);
	}

	private static void ApplyMissionRewards(
		GlobeTeamHolder playerTeam,
		MissionCellDefinition missionDefinition,
		Enums.MissionStatus outcome)
	{
		if (missionDefinition.HasBattleResult)
		{
			MissionScoreBreakdown score = missionDefinition.BattleScore;
			AddScore(
				playerTeam,
				score.EnemyKillPoints,
				Enums.MonthlyScoreReason.EnemyUnitsKilled);
			AddScore(
				playerTeam,
				score.UnitLossPoints,
				Enums.MonthlyScoreReason.PlayerUnitsLost);
			AddScore(
				playerTeam,
				score.OutcomePoints,
				GetMonthlyScoreReason(outcome));

			if (missionDefinition.RecoverySaleProceeds > 0)
				playerTeam.ChangeFunds(
					missionDefinition.RecoverySaleProceeds,
					"Recovered equipment sales");
			return;
		}

		int outcomeScore = missionDefinition.mission != null
			? missionDefinition.mission.GetOutcomePoints(outcome)
			: missionDefinition.scoreChange.TryGetValue(
				outcome,
				out int legacyScore)
				? legacyScore
				: 0;
		AddScore(playerTeam, outcomeScore, GetMonthlyScoreReason(outcome));
	}

	private static void AddScore(
		GlobeTeamHolder playerTeam,
		int score,
		Enums.MonthlyScoreReason reason)
	{
		if (score != 0) playerTeam.AddMonthlyScore(score, reason);
	}

	private void ApplyFailedMissionOpinionPenalty(
		MissionCellDefinition missionDefinition)
	{
		if (missionDefinition?.mission == null) return;

		var country = GlobeHexGridManager.Instance?.GetCountryStateForIndex(
			missionDefinition.cellIndex);
		if (country == null) return;

		float opinionLoss = GetFailedMissionOpinionLoss(
			missionDefinition.mission.MissionType);
		if (opinionLoss <= 0.0f) return;

		country.ChangePlayerOpinion(-opinionLoss);
	}

	public float GetFailedMissionOpinionLoss(Enums.MissionType missionType)
	{
		if (failedMissionOpinionLoss != null &&
		    failedMissionOpinionLoss.TryGetValue(missionType, out float loss))
		{
			return Mathf.Max(0.0f, loss);
		}

		return failedMissionOpinionLoss != null &&
		       failedMissionOpinionLoss.TryGetValue(
			       Enums.MissionType.None,
			       out float fallbackLoss)
			? Mathf.Max(0.0f, fallbackLoss)
			: 0.0f;
	}

    private static Enums.MissionStatus GetMissionOutcome(MissionCellDefinition mission)
    {
	    if (mission == null) return Enums.MissionStatus.None;
	    
	    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Visited))
	    {
		    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Successful))
			    return Enums.MissionStatus.Successful;
		    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Aborted))
			    return Enums.MissionStatus.Aborted;
		    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Failed))
			    return Enums.MissionStatus.Failed;
		    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Timeout))
			    return Enums.MissionStatus.Timeout;
	    }
	    else if (mission.timeLeft <= 0)
	    {
		    return Enums.MissionStatus.Timeout;
	    }
        return Enums.MissionStatus.None;
    }

    private static Enums.MonthlyScoreReason GetMonthlyScoreReason(Enums.MissionStatus outcome)
    {
	    return outcome switch
	    {
			Enums.MissionStatus.Successful => Enums.MonthlyScoreReason.SuccessfulMission,
			Enums.MissionStatus.Failed => Enums.MonthlyScoreReason.FailedMission,
			Enums.MissionStatus.Timeout => Enums.MonthlyScoreReason.ExpiredMission,
			Enums.MissionStatus.Aborted => Enums.MonthlyScoreReason.AbandonedMission,
			_ => Enums.MonthlyScoreReason.None
	    };
    }

    private static Craft FindMissionCraft(GlobeTeamHolder playerTeam, Craft savedCraft)
    {
        if (savedCraft == null)
            return null;

        // Craft indices are scoped to a base, so use the saved home-base index
        // first and fall back to checking every player base for older saves.
        foreach (TeamBaseCellDefinition baseDefinition in playerTeam.Bases)
        {
            if (baseDefinition.cellIndex == savedCraft.HomeBaseIndex &&
                baseDefinition.TryGetCraftFromIndex(savedCraft.Index, out Craft craft))
            {
                return craft;
            }
        }

        foreach (TeamBaseCellDefinition baseDefinition in playerTeam.Bases)
        {
            if (baseDefinition.TryGetCraftFromIndex(savedCraft.Index, out Craft craft))
                return craft;
        }

        GD.PrintErr($"Could not find craft {savedCraft.Index} for visited mission {savedCraft.TargetCellIndex}.");
        return null;
    }

    private void DestroyMissionVisual(MissionCellDefinition mission)
    {
	    if (mission?.missionVisual == null
	        || !GodotObject.IsInstanceValid(mission.missionVisual))
		    return;

	    Node visual = mission.missionVisual;
	    visual.GetParent()?.RemoveChild(visual);
	    visual.QueueFree();
	    mission.missionVisual = null;
    }
    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        var data = new Godot.Collections.Dictionary<string, Variant>
        {
            { "globalDifficulty", GlobalDifficulty },
            { "timer", _currentMissionTimer }
        };

        var missionListData = new Godot.Collections.Dictionary<string, Variant>();
        foreach (var kvp in _activeMissions)
        {
            missionListData.Add(kvp.Key.ToString(), kvp.Value.Save());
        }

        data.Add("activeMissions", missionListData);
        return data;
    }

    public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        if (!HasLoadedData)
	        return Task.CompletedTask;

        if (missionContainer != null)
        {
            foreach (Node child in missionContainer.GetChildren())
                child.QueueFree();
        }
		foreach (MissionCellDefinition missionDefinition in _activeMissions.Values)
			missionDefinition.StopTimeoutTracking();
        _activeMissions.Clear();

        GlobalDifficulty = data.ContainsKey("globalDifficulty")
            ? data["globalDifficulty"].AsInt32()
            : 1;
        _currentMissionTimer = data.ContainsKey("timer")
            ? data["timer"].AsSingle()
            : missionInterval;

        if (!data.ContainsKey("activeMissions"))
	        return Task.CompletedTask;

        var missionListData = data["activeMissions"].AsGodotDictionary<string, Variant>();

        foreach (var kvp in missionListData)
        {
            int cellIdx = int.Parse(kvp.Key);

			var mDefData = kvp.Value.AsGodotDictionary<string, Variant>();
			var mData = mDefData["missionData"].AsGodotDictionary<string, Variant>();
			string className = mDefData["missionClass"].AsString();
			string definitionName = mDefData.ContainsKey("definitionName")
				? mDefData["definitionName"].AsString()
				: "New Mission";

            MissionBase mission = null;

            Enums.MissionType type = (Enums.MissionType)mData["type"].AsInt32();
			Enums.MissionRecoveryType recoveryType = mData.ContainsKey("recoveryType")
				? (Enums.MissionRecoveryType)mData["recoveryType"].AsInt32()
				: Enums.MissionRecoveryType.FullFieldOnSuccess;
			string missionName = mData.ContainsKey("name")
				? mData["name"].AsString()
				: "Mission";
			string missionDescription = mData.ContainsKey("description")
				? mData["description"].AsString()
				: string.Empty;
			int difficulty = mData.ContainsKey("difficulty")
				? mData["difficulty"].AsInt32()
				: 1;
            Enums.MissionStatus status = (Enums.MissionStatus)mDefData["missionStatus"].AsInt32();

			int timeoutTime = mDefData.ContainsKey("timeoutTime")
				? mDefData["timeoutTime"].AsInt32()
				: 12;
			int timeLeft = mDefData.ContainsKey("timeLeft")
				? mDefData["timeLeft"].AsInt32()
				: timeoutTime;
			int alienOperationId = mDefData.ContainsKey("alienOperationId")
				? mDefData["alienOperationId"].AsInt32()
				: -1;
            Craft onRouteCraft = null;
            if (mDefData.ContainsKey("onRouteCraft"))
            {
	            var savedCraftData = mDefData["onRouteCraft"].AsGodotDictionary<string, Variant>();
	            if (savedCraftData.Count > 0)
	            {
	                onRouteCraft = new Craft();
	                onRouteCraft.Load(savedCraftData);
	            }
            }
            int count = mData["enemyCount"].AsInt32();

            if (className == nameof(EliminateMission))
            {
                mission = new EliminateMission(
	                missionName,
	                missionDescription,
	                type,
	                difficulty,
	                count,
	                cellIdx,
	                recoveryType);
            }

			if (mission != null)
			{
				mission.RestoreScoring(mData);
				var missionDefinition = new MissionCellDefinition(
					cellIdx,
					definitionName,
					mission,
					null,
					status,
					onRouteCraft,
					alienOperationId);
				missionDefinition.RestoreTimeoutState(timeoutTime, timeLeft);
				missionDefinition.RestoreBattleResult(mDefData);
				_activeMissions.Add(cellIdx, missionDefinition);
			}
        }
        return Task.CompletedTask;
    }

    public override void Deinitialize()
    {
		foreach (MissionCellDefinition missionDefinition in _activeMissions.Values)
			missionDefinition.StopTimeoutTracking();
    }

    #region Get/Set Functions

    public System.Collections.Generic.Dictionary<int, MissionCellDefinition> GetActiveMissions() => _activeMissions;

    #endregion
}
