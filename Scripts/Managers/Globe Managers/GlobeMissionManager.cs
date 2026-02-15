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
    [Export] private int missionSpawnRangeSteps = 4;
    [Export] private int maxActiveMissions = 8;

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
            foreach (var missionDef in _activeMissions.Values)
            {
                var cell = GlobeHexGridManager.Instance.GetCellFromIndex(
                    missionDef.cellIndex
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
        if (Engine.IsEditorHint())
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
        if (_activeMissions.Count >= maxActiveMissions)
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
                missionSpawnRangeSteps
            );

            foreach (var cell in cellsInRange)
            {
                if (
                    cell.cellType == Enums.HexGridType.Land
                    && !occupiedIndices.Contains(cell.Index)
                )
                {
                    candidateCellIndices.Add(cell.Index);
                }
            }
        }

        if (candidateCellIndices.Count > 0)
        {
            int[] candidates = candidateCellIndices.ToArray();
            int randomIndex = candidates[GD.RandRange(0, candidates.Length - 1)];

            HexCellData? targetCell = gridManager.GetCellFromIndex(randomIndex);

            if (targetCell.HasValue)
            {
                TrySpawnNewMissionCell(targetCell.Value, Enums.MissionType.None);
            }
        }
    }

    public bool TrySpawnNewMissionCell(HexCellData cell, Enums.MissionType missionType)
    {
        if (_activeMissions.ContainsKey(cell.Index))
            return false;

        MissionBase mission = GenerateRandomMission(cell.Index, missionType);
        if (mission == null)
            return false;

        MissionCellDefinition missionCellDefinition = new MissionCellDefinition(
            cell.Index,
            "New Mission",
            mission,
          null
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
        GameManager.Instance.unitCounts = new Vector2I(
            2,
            missionDefinition.mission.EnemySpawnCount
        );
        GameManager.Instance.TryChangeScene(GameManager.GameScene.BattleScene, null, true);
        RemoveMissionDefinition(missionDefinition.cellIndex);
    }



    private Node3D SpawnMissionVisual(HexCellData cell, string name)
    {
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


    private void RemoveMissionDefinition(int cellIndex)
    {
	    if (!_activeMissions.ContainsKey(cellIndex)) return;
	    
	    MissionCellDefinition missionDefinition = _activeMissions[cellIndex];
	    DestroyMissionVisual(cellIndex);
	    _activeMissions.Remove(cellIndex);
    }

    private void DestroyMissionVisual(int cellIndex)
    {
	    MissionCellDefinition missionDefinition = _activeMissions[cellIndex];
	    
	    if (missionDefinition == null) return;
	    
	    
	    if (missionContainer != null)
	    {
		    missionContainer.RemoveChild(missionDefinition.missionVisual);

	    }
	    else
	    {
		    RemoveChild(missionDefinition.missionVisual);
	    }
	    missionDefinition.missionVisual.QueueFree();
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

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        base.Load(data);
        if (!HasLoadedData)
            return;

        if (missionContainer != null)
        {
            foreach (Node child in missionContainer.GetChildren())
                child.QueueFree();
        }
        _activeMissions.Clear();

        GlobalDifficulty = data.ContainsKey("globalDifficulty")
            ? data["globalDifficulty"].AsInt32()
            : 1;
        _currentMissionTimer = data.ContainsKey("timer")
            ? data["timer"].AsSingle()
            : missionInterval;

        if (!data.ContainsKey("activeMissions"))
            return;

        var missionListData = data["activeMissions"].AsGodotDictionary<string, Variant>();

        foreach (var kvp in missionListData)
        {
            int cellIdx = int.Parse(kvp.Key);

            var mDefData = kvp.Value.AsGodotDictionary<string, Variant>();
            var mData = mDefData["missionData"].AsGodotDictionary<string, Variant>();
            string className = mDefData["missionClass"].AsString();

            MissionBase mission = null;
            Enums.MissionType type = (Enums.MissionType)mData["type"].AsInt32();
            int count = mData["enemyCount"].AsInt32();

            if (className == nameof(EliminateMission))
            {
                mission = new EliminateMission(type, count, cellIdx);
            }

            if (mission != null)
            {
                _activeMissions.Add(
                    cellIdx,
                    new MissionCellDefinition(cellIdx, "New Mission", mission)
                );
            }
        }
    }

    public override void Deinitialize()
    {
	    return;
    }

    #region Get/Set Functions

    public System.Collections.Generic.Dictionary<int, MissionCellDefinition> GetActiveMissions() => _activeMissions;

    #endregion
}