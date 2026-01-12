using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class GlobeMissionManager : Manager<GlobeMissionManager>
{
    public int GlobalDifficulty { get; set; } = 1;
    public bool sendMissionMode = false;

    
    private System.Collections.Generic.Dictionary<int, MissionCellDefinition> activeMissions = new();

    [Export] private float missionInterval = 5.0f;
    [Export] private int missionSpawnRangeSteps = 4; 
    private float currentMissionTimer;
    
    [Export] private PackedScene missionScene;
    [Export] private Node missionContainer;

    #region Signals
    
    [Signal] public delegate void MissionSpawnedEventHandler(MissionBase mission);
    [Signal] public delegate void MissionCompletedEventHandler();
    #endregion
    
    public override string GetManagerName() => "GlobeMissionManager";

    protected override async Task _Setup(bool loadingData)
    {
	    if (!loadingData)
	    {
		    activeMissions = new System.Collections.Generic.Dictionary<int, MissionCellDefinition>();
		    currentMissionTimer = missionInterval;
	    }
	    await Task.CompletedTask;
    }

    protected override async Task _Execute(bool loadingData)
    {
	    if (loadingData)
	    {
		    foreach (var missionDef in activeMissions.Values)
		    {
			    var cell = GlobeHexGridManager.Instance.GetCellFromIndex(missionDef.cellIndex);
			    if (cell.HasValue)
			    {
				    SpawnMissionVisual(cell.Value, $"Mission_{missionDef.cellIndex}");
			    }
		    }
	    }
    }
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;

        currentMissionTimer -= (float)delta;
        
        if (currentMissionTimer <= 0f)
        {
            AttemptSpawnMissionNearPlayer();
            currentMissionTimer = missionInterval;
        }
    }

    private void AttemptSpawnMissionNearPlayer()
    {
        var gridManager = GlobeHexGridManager.Instance;
        var teamManager = GlobeTeamManager.Instance;

        if (gridManager == null || teamManager == null) return;
        
        var allTeams = teamManager.GetAllTeamData();
        if (!allTeams.ContainsKey(Enums.UnitTeam.Player)) return;

        var playerTeamData = allTeams[Enums.UnitTeam.Player];
        if (playerTeamData.Bases.Count == 0) return;
        
        HashSet<int> candidateCellIndices = new HashSet<int>();
        HashSet<int> occupiedIndices = new HashSet<int>();
        
        foreach (var b in playerTeamData.Bases) occupiedIndices.Add(b.cellIndex);
        
        foreach (var k in activeMissions.Keys) occupiedIndices.Add(k);

        foreach (var baseDef in playerTeamData.Bases)
        {
            var baseCell = gridManager.GetCellFromIndex(baseDef.cellIndex);
            if (baseCell == null) continue;
            
            List<HexCellData> cellsInRange = gridManager.GetCellsInStepRange(baseCell.Value, missionSpawnRangeSteps);

            foreach (var cell in cellsInRange)
            {
                if (cell.cellType == Enums.HexGridType.Land && !occupiedIndices.Contains(cell.Index))
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
                if(TrySpawnNewMissionCell(targetCell.Value, Enums.MissionType.None))
					GD.Print($"Spawned mission at Cell {randomIndex}");
               
            }
        }
        else
        {
            GD.Print("No valid location found to spawn mission near player base.");
        }
    }

    public bool TrySpawnNewMissionCell(HexCellData cell, Enums.MissionType missionType)
    {
        if (activeMissions.ContainsKey(cell.Index)) return false;

        MissionBase mission = GenerateRandomMission(cell.Index ,missionType);
        
        if (mission == null) return false;

        MissionCellDefinition missionCellDefinition = new MissionCellDefinition(cell.Index, mission);
        
        activeMissions.Add(cell.Index, missionCellDefinition);
        
        SpawnMissionVisual(cell, $"Mission_{cell.Index}");
        EmitSignal(SignalName.MissionSpawned, mission);
        return true;
    }

    public MissionBase GenerateRandomMission(int cellIndex,
        Enums.MissionType missionType = Enums.MissionType.None,
        int difficulty = -1)
    {
        int enemyCount = -1;
        
        if (missionType == Enums.MissionType.None)
        {
            // Pick random mission type
            var values = Enum.GetValues<Enums.MissionType>();
            int missionIndex = GD.RandRange(1, values.Length - 1); 
            missionType = values[missionIndex];
        }

        if (difficulty == -1)
        {
            int minDifficulty = Math.Clamp(GlobalDifficulty - 2, 1, 10);
            int maxDifficulty = Math.Clamp(GlobalDifficulty + 2, 1, 10);
            difficulty = GD.RandRange(minDifficulty, maxDifficulty);
        }


        
        if (difficulty is >= 1 and <= 4)
            enemyCount = GD.RandRange(1, 3);
        else if (difficulty is >= 5 and <= 7)
            enemyCount = GD.RandRange(3, 6);
        else if (difficulty is >= 8 and <= 10)
            enemyCount = GD.RandRange(4, 10);
        else 
            enemyCount = 1;
        
        
        GD.Print($"Difficulty: {difficulty}, enemy count: {enemyCount}");
        MissionBase retMission = new EliminateMission(missionType, enemyCount, cellIndex);
        
        return retMission;
    }
    
    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        var data = new Godot.Collections.Dictionary<string, Variant>();
        data.Add("globalDifficulty", GlobalDifficulty);
        data.Add("timer", currentMissionTimer);

        var missionListData = new Godot.Collections.Dictionary<string, Variant>();
        foreach (var kvp in activeMissions)
        {
            missionListData.Add(kvp.Key.ToString(), kvp.Value.Save());
        }

        data.Add("activeMissions", missionListData);
        return data;
    }

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
	    
	    base.Load(data);
	    if(!HasLoadedData) return;
	    
        if (missionContainer != null)
        {
            foreach (Node child in missionContainer.GetChildren()) child.QueueFree();
        }
        activeMissions.Clear();
        
        
        GlobalDifficulty = data.ContainsKey("globalDifficulty") ? data["globalDifficulty"].AsInt32() : 1;
        currentMissionTimer = data.ContainsKey("timer") ? data["timer"].AsSingle() : missionInterval;

        if (!data.ContainsKey("activeMissions")) return;

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
                activeMissions.Add(cellIdx, new MissionCellDefinition(cellIdx, mission));
            }
        }
    }
    
    private void SpawnMissionVisual(HexCellData cell, string name)
    {
	    if (missionScene == null) return;

	    Node3D missionInstance = missionScene.Instantiate<Node3D>();
	    if (missionContainer != null) missionContainer.AddChild(missionInstance);
	    else AddChild(missionInstance);

	    missionInstance.GlobalPosition = cell.Center;

	    Vector3 surfaceNormal = cell.Center.Normalized();
	    Vector3 upDir = Mathf.Abs(surfaceNormal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
	    missionInstance.LookAt(cell.Center + surfaceNormal, upDir);

	    missionInstance.Name = name;
    }

    public override void _Input(InputEvent @event)
    {
	    base._Input(@event);
	    if (!sendMissionMode) return;

	    if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed &&
	        mouseButton.ButtonIndex == MouseButton.Left)
	    {
		    HexCellData? cell = GlobeInputManager.Instance.CurrentCell;
		    if (cell == null) return;
		    if (!activeMissions.ContainsKey(cell.Value.Index)) return;

		    LoadMissionScene(activeMissions[cell.Value.Index]);
	    }
    }

    public void LoadMissionScene(MissionCellDefinition missionDefinition)
    {
	    if (missionDefinition == null) return;

	    GameManager.Instance.unitCounts = new Vector2I(2, missionDefinition.mission.EnemySpawnCount);
	    GD.Print($"Loading mission with {GameManager.Instance.unitCounts}");
	    
	    GameManager.Instance.TryChangeScene(GameManager.GameScene.BattleScene, 
		    null, true);
    }
    
    public System.Collections.Generic.Dictionary<int, MissionCellDefinition>  GetActiveMissions() => activeMissions;
    
    public override void Deinitialize()
    {
	    return;
    }
}