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
		// Kept as an opt-in testing tool. Normal campaign missions are initiated
		// by GlobeAIManager operations rather than real-time random spawning.
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

		MissionBase mission = GenerateRandomMission(cell.Index, missionType, difficulty);
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
        int difficulty = -1)
    {
        int enemyCount;

        if (missionType == Enums.MissionType.None)
        {
            var values = Enum.GetValues<Enums.MissionType>();
            // Exclude 'None' if it is at index 0
            int missionIndex = GD.RandRange(1, values.Length - 1);
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

        return new EliminateMission(missionType, enemyCount, cellIndex);
    }
    

    public void LoadMissionScene(MissionCellDefinition missionDefinition)
    {
	    // Save the complete Globe state NOW, before leaving
	    SavesManager.Instance.SetSessionData("GlobeState", SavesManager.Instance.GetSceneTransitionState());

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

	    GameManager.Instance.TryChangeScene(
		    GameManager.GameScene.BattleScene,
		    saveManagerData: false  
	    );
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
	    if (missionDefinition == null) return;

        GlobeTeamManager teamManager = GlobeTeamManager.Instance;
        GlobeTeamHolder playerTeam = teamManager?.GetTeamData(Enums.UnitTeam.Player);

		Enums.MissionStatus outcome = GetMissionOutcome(missionDefinition);
		if (missionDefinition.alienOperationId >= 0 && outcome != Enums.MissionStatus.None)
			GlobeAIManager.Instance?.ResolveOperation(
				missionDefinition.alienOperationId,
				outcome);
		if (playerTeam != null && outcome != Enums.MissionStatus.None &&
            missionDefinition.scoreChange.TryGetValue(outcome, out int scoreChange))
        {
            playerTeam.AddMonthlyScore(scoreChange);
        }

		Craft craft = playerTeam == null
			? null
			: FindMissionCraft(playerTeam, missionDefinition.onRouteCraft);
        
        if (craft != null)
        {
	        if (craft.CurrentCellIndex != craft.HomeBaseIndex)
	        {
		        TeamBaseCellDefinition homeBase = craft.GetBaseCellDefinition();
		        if (homeBase == null)
			        return;

		        _ = homeBase.SendCraft(
			        craft.CurrentCellIndex,
			        craft.HomeBaseIndex,
			        craft,
			        teamManager
		        );
	        }
        }
        
        RemoveMissionDefinition(missionDefinition);
    }

    private static Enums.MissionStatus GetMissionOutcome(MissionCellDefinition mission)
    {
	    if (mission == null) return Enums.MissionStatus.None;
	    
	    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Visited))
	    {
		    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Successful))
			    return Enums.MissionStatus.Successful;
		    if (mission.missionStatus.HasFlag(Enums.MissionStatus.Failed))
			    return Enums.MissionStatus.Failed;
	    }
	    else if (mission.timeLeft <= 0)
	    {
		    return Enums.MissionStatus.Timeout;
	    }
        return Enums.MissionStatus.None;
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
                mission = new EliminateMission(type, count, cellIdx);
            }

            if (mission != null)
            {
				var missionDefinition = new MissionCellDefinition(
					cellIdx,
					definitionName,
					mission,
					null,
					status,
					onRouteCraft,
					alienOperationId
				);
				missionDefinition.RestoreTimeoutState(timeoutTime, timeLeft);
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
