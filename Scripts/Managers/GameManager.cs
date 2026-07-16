using System;
using System.Collections.Generic;
using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

public partial class GameManager : Manager<GameManager>
{
	#region Variables / Properties

	// Track managers in the current active scene
	public Array<ManagerBase> activeSceneManagers = new();
	private List<ManagerBase> _persistentGlobals = new();


	public static readonly Godot.Collections.Dictionary<GameScene, string> scenePaths = new()
	{
		{ GameScene.BattleScene, "res://Scenes/GameScenes/BattleScene.tscn" },
		{ GameScene.GlobeScene, "res://Scenes/GameScenes/GlobeScene.tscn" },
		{ GameScene.MainMenu, "res://Scenes/GameScenes/MainMenuScene.tscn" },
		{ GameScene.BaseScene, "res://Scenes/GameScenes/BaseScene.tscn" }
	};

	public enum GameScene
	{
		NONE,
		MainMenu,
		BattleScene,
		GlobeScene,
		BaseScene
	}

	public enum LoadingState
	{
		NONE,
		CHANGINGSCENES,
		SETTINGUPMANAGERS,
		EXECUTINGMANAGERS
	}

	[Export] public GameScene currentScene;
	public Vector2I mapSize = new Vector2I(1, 1);
	public Vector2I unitCounts = new Vector2I(2, 2);
	public MissionCellDefinition currentMission;
	public TeamBaseCellDefinition currentBase;
	public int currentBaseFunds;
	public PackedScene unitScene;
	private Godot.Collections.Array<
		Godot.Collections.Dictionary<string, Variant>> _pendingBattlePlayerUnits;

	public bool HasPendingBattlePlayerUnits => _pendingBattlePlayerUnits != null;

	public float loadingPercent = 0;
	public LoadingState loadingState = LoadingState.NONE;
	public string loadingManagerName = "";

	#endregion

	[Signal]
	public delegate void CoreManagersLoadedEventHandler();
	[Signal] public delegate void SceneChangedEventHandler(GameScene scene);

	public override string GetManagerName() => "GameManager";

	public override void _Ready()
	{
		DebugMode = true;
		base._Ready();
		unitScene = ResourceLoader.Load<PackedScene>("res://Scenes/GridObjects/Unit.tscn");

		_ = InitialBootSequence();
	}

	private async Task InitialBootSequence()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GatherManagersInCurrentScene();
		await SetupAndExecuteSequence(false);
	}

	#region Lifecycle Implementation

	/// <summary>
	/// GameManager's own setup. Orchestration of child managers is now handled in SetupAndExecuteSequence.
	/// </summary>
	protected override async Task _Setup(bool loadingData)
	{
		await Task.CompletedTask;
	}

	/// <summary>
	/// GameManager's own execution phase. Orchestration of child managers is now handled in SetupAndExecuteSequence.
	/// </summary>
	protected override async Task _Execute(bool loadingData)
	{
		await Task.CompletedTask;
	}

	public override void Deinitialize()
	{
		GD.Print("[Cleanup] GameManager Autoload Deinitialized.");
	}

	public void CleanupManagers()
	{
		GD.Print($"[Cleanup] Deinitializing scene managers for currentScene...");
		foreach (var m in activeSceneManagers)
		{
			if (GodotObject.IsInstanceValid(m))
				m.Deinitialize();
		}
	}

	#endregion

	#region Scene Management & Transitions

	public async Task<bool> StartNewGame(GameScene scene, string tempSaveName = "new SaveGame")
	{
		if (!scenePaths.ContainsKey(scene)) return false;

		SavesManager.LoadFromAutosave = false;
		SavesManager.PendingSaveData = null;
		SavesManager.Instance.currentSavename = tempSaveName;

		await ChangeSceneAsync(scene, false);
		return true;
	}

	public async Task<bool> TryChangeScene(GameScene sceneName, bool saveManagerData = true, bool loadSceneData = true)
	{
		if (!scenePaths.ContainsKey(sceneName)) return false;

		var sm = SavesManager.Instance;
		if (saveManagerData)
		{
			string saveKey = sm.currentSavename.Contains("quickplay_internal") ? "quickplay_internal" : "autosave";
			sm.SaveGame(saveKey, sceneName);
			SavesManager.LoadFromAutosave = true;
		}
		else
		{
			SavesManager.LoadFromAutosave = false;
			SavesManager.PendingSaveData = sm.PackageFullState();
			SavesManager.PendingSaveName = sm.currentSavename;
		}

		await ChangeSceneAsync(sceneName, true);
		return true;
	}

	/// <summary>
	/// The core transition worker for the Autoload.
	/// </summary>
	public async Task ChangeSceneAsync(GameScene scene, bool loadingData)
	{
		loadingState = LoadingState.CHANGINGSCENES;
		loadingPercent = 0;
		loadingManagerName = "Scene Transition";
		UIManager.Instance?.ShowLoadingScreen();

		CleanupManagers();
		currentScene = scene;

		Error err = GetTree().ChangeSceneToFile(scenePaths[scene]);
		if (err != Error.Ok) return;

		// Wait for nodes to enter tree
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		GatherManagersInCurrentScene();
		await SetupAndExecuteSequence(loadingData);

		// SetupAndExecuteSequence may call GameManager.Load() with a state snapshot that
		// was captured BEFORE this transition started (see TryChangeScene's
		// saveManagerData:false branch, which packages full state pre-transition). That
		// snapshot's "currentScene" reflects the OLD scene, so it can clobber the
		// assignment above. The scene we were actually asked to switch to is always the
		// source of truth here, so re-assert it.
		currentScene = scene;
		EmitSignal(SignalName.SceneChanged, (int)currentScene);
	}

	public void RegisterGlobalManager(ManagerBase manager)
	{
		if (!_persistentGlobals.Contains(manager))
		{
			_persistentGlobals.Add(manager);
			GD.Print($"[GameManager] Registered Global: {manager.GetManagerName()}");
		}
	}

	private void GatherManagersInCurrentScene()
	{
		activeSceneManagers.Clear();

		// Add the persistent globals first
		foreach (var gm in _persistentGlobals)
		{
			if (IsInstanceValid(gm) && gm != this) activeSceneManagers.Add(gm);
		}

		// Find local managers only within the current scene root
		var sceneRoot = GetTree().CurrentScene;
		if (sceneRoot != null)
		{
			// Recursively find nodes, but skip them if they are the GameManager itself
			foreach (var node in FindManagersRecursive(sceneRoot))
			{
				// Avoid adding same manager twice if discovery finds a global for some reason
				if (!activeSceneManagers.Contains(node))
				{
					activeSceneManagers.Add(node);
				}
			}
		}
	}

	private List<ManagerBase> FindManagersRecursive(Node root)
	{
		List<ManagerBase> found = new();
		if (root is ManagerBase mb && mb != this) found.Add(mb);
		foreach (Node child in root.GetChildren())
			found.AddRange(FindManagersRecursive(child));
		return found;
	}

	private async Task SetupAndExecuteSequence(bool loadingData)
	{
		var rootData = SavesManager.PendingSaveData;
		var managersDict = (loadingData && rootData != null && rootData.ContainsKey("managers"))
			? rootData["managers"].AsGodotDictionary<string, Variant>()
			: null;

		// Load GameManager's own data
		if (managersDict != null && managersDict.ContainsKey(GetManagerName()))
			await LoadCall(managersDict[GetManagerName()].AsGodotDictionary<string, Variant>());

		// Filter list
		var cleanList = new List<ManagerBase>();
		foreach (var m in activeSceneManagers)
			if (IsInstanceValid(m) && !m.IsQueuedForDeletion())
				cleanList.Add(m);

		// Calculate progress for managers that are actually going to run
		int stepsToCompute = 0;
		foreach (var m in cleanList)
		{
			// We only count it in progress if it hasn't initialized OR if it repeats
			if ((!m.HasInitialized || !m.ShouldExecuteOnlyOnce) && m.includeInLoadingCalculation)
				stepsToCompute++;
		}

		// +1 represents GameManager's own setup and execution steps
		int totalSteps = (stepsToCompute + 1) * 2;
		int completedSteps = 0;

		// ---------- SETUP PHASE ----------
		loadingState = LoadingState.SETTINGUPMANAGERS;
		loadingPercent = 0f;

		loadingManagerName = GetManagerName();
		await this.SetupCall(loadingData);
		completedSteps++;
		loadingPercent = (float)completedSteps / totalSteps;

		foreach (var m in cleanList)
		{
			loadingManagerName = m.GetManagerName();

			// 1. ALWAYS LOAD: Catch state changes even for persistent managers
			if (managersDict != null && managersDict.ContainsKey(loadingManagerName))
				await m.LoadCall(managersDict[loadingManagerName].AsGodotDictionary<string, Variant>());

			if (!m.HasInitialized || !m.ShouldExecuteOnlyOnce)
			{
				if (DebugMode) GD.Print($"[GameManager] Setting up: {loadingManagerName}");
				await m.SetupCall(loadingData);
				if (m.includeInLoadingCalculation) completedSteps++;
			}
			else
			{
				if (DebugMode) GD.Print($"[GameManager] Skipping Setup (Already Initialized): {loadingManagerName}");
			}

			loadingPercent = (float)completedSteps / totalSteps;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		// ---------- EXECUTION PHASE ----------
		loadingState = LoadingState.EXECUTINGMANAGERS;

		loadingManagerName = GetManagerName();
		await this.ExecuteCall(loadingData);
		completedSteps++;
		loadingPercent = (float)completedSteps / totalSteps;

		foreach (var m in cleanList)
		{
			loadingManagerName = m.GetManagerName();

			// CONDITIONALLY EXECUTE
			if (!m.HasInitialized || !m.ShouldExecuteOnlyOnce)
			{
				if (DebugMode) GD.Print($"[GameManager] Executing: {loadingManagerName}");
				await m.ExecuteCall(loadingData);
				m.HasInitialized = true; // Mark as done forever (if ShouldExecuteOnlyOnce is true)
				if (m.includeInLoadingCalculation) completedSteps++;
			}
			else
			{
				if (DebugMode)
					GD.Print($"[GameManager] Skipping Execution (Already Initialized): {loadingManagerName}");
			}

			loadingPercent = (float)completedSteps / totalSteps;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		loadingManagerName = "";
		loadingState = LoadingState.NONE;
		loadingPercent = 1.0f;
		EmitSignal(SignalName.CoreManagersLoaded);
		SavesManager.PendingSaveData = null;
	}

	#endregion

	#region Game Logic & Rules

	public void PrepareBattleLoadout(Craft craft)
	{
		_pendingBattlePlayerUnits = craft == null
			? new Godot.Collections.Array<
				Godot.Collections.Dictionary<string, Variant>>()
			: GridObjectSerializationUtility.SaveGridObjects(
				craft.GetStationedGridObjects()
			);

		unitCounts = new Vector2I(_pendingBattlePlayerUnits.Count, unitCounts.Y);
		InventoryManager.Instance?.SetStartingItems(craft?.GetItemCounts);
	}

	public Godot.Collections.Array<
		Godot.Collections.Dictionary<string, Variant>> GetPendingBattlePlayerUnits()
	{
		return _pendingBattlePlayerUnits;
	}

	public void ClearPendingBattleLoadout()
	{
		_pendingBattlePlayerUnits = null;
		InventoryManager.Instance?.ResetStartingItemsToDefaults();
	}

	public void CheckGameState(Turn currentTurn)
	{
		var teamHolders = GridObjectManager.Instance.GetGridObjectTeamHolders();
		foreach (var kvp in teamHolders)
		{
			if (kvp.Value == null) continue;
			if (kvp.Key == Enums.UnitTeam.Enemy || kvp.Key == Enums.UnitTeam.Player)
			{
				if (kvp.Value.GridObjects[Enums.GridObjectState.Active].Count < 1)
				{
					if (currentMission != null)
					{
						// Preserve the visit flag recorded when the battle began, while
						// replacing the temporary OnRoute flag with the final outcome.
						currentMission.missionStatus &= ~Enums.MissionStatus.OnRoute;
						currentMission.missionStatus |= Enums.MissionStatus.Visited;
						currentMission.missionStatus |= kvp.Key == Enums.UnitTeam.Player
							? Enums.MissionStatus.Failed
							: Enums.MissionStatus.Successful;
					}

					EndGame();
					return;
				}
			}
		}
	}

	private async void EndGame()
	{
		if (SavesManager.Instance.currentSavename == "quickplay_internal")
		{
			await ChangeSceneAsync(GameScene.MainMenu, false);
		}
		else
		{
			await EndBattleAndReturnToGlobe();
		}
	}

	public async Task EndBattleAndReturnToGlobe()
	{
		// Pull from memory instead of disk
		var globeData = SavesManager.Instance.ConsumeSceneState("GlobeState");
		if (globeData == null) return;

		if (currentMission != null)
			UpdateMissionStatusInSavedData(globeData, currentMission);

		SavesManager.PendingSaveData = globeData;
		SavesManager.LoadFromAutosave = false;
		currentMission = null;

		await ChangeSceneAsync(GameScene.GlobeScene, true);
	}

	public async Task ReturnToGlobe()
	{
		// Pull from memory instead of disk
		var globeData = SavesManager.Instance.ConsumeSceneState("GlobeState");
		if (globeData == null) return;

		MergeCurrentBaseIntoGlobeState(globeData);

		SavesManager.PendingSaveData = globeData;
		SavesManager.LoadFromAutosave = false;

		await ChangeSceneAsync(GameScene.GlobeScene, true);
	}

	public bool SyncCurrentBaseToGlobeState()
	{
		if (SavesManager.Instance == null ||
		    !SavesManager.Instance.TryGetSessionData("GlobeState", out Variant stateValue))
			return false;

		var globeData = stateValue.AsGodotDictionary<string, Variant>();
		MergeCurrentBaseIntoGlobeState(globeData);
		SavesManager.Instance.SetSessionData("GlobeState", globeData);
		return true;
	}

	private void MergeCurrentBaseIntoGlobeState(
		Godot.Collections.Dictionary<string, Variant> globeData)
	{
		if (currentBase == null) return;
		if (!globeData.TryGetValue("managers", out Variant managersValue)) return;

		var managers = managersValue.AsGodotDictionary<string, Variant>();
		if (!managers.TryGetValue("GlobeTeamManager", out Variant teamManagerValue)) return;

		var teamManagerData = teamManagerValue.AsGodotDictionary<string, Variant>();
		if (!teamManagerData.TryGetValue("teamData", out Variant teamDataValue)) return;

		var teamData = teamDataValue.AsGodotDictionary<string, Variant>();
		string teamKey = ((int)currentBase.teamAffiliation).ToString();
		if (!teamData.TryGetValue(teamKey, out Variant holderValue)) return;

		var holderData = holderValue.AsGodotDictionary<string, Variant>();
		if (!holderData.TryGetValue("bases", out Variant basesValue)) return;

		var bases = basesValue.AsGodotDictionary<string, Variant>();
		bases[currentBase.cellIndex.ToString()] = currentBase.Save();
		holderData["bases"] = bases;
		holderData["funds"] = currentBaseFunds;
		teamData[teamKey] = holderData;
		teamManagerData["teamData"] = teamData;
		managers["GlobeTeamManager"] = teamManagerData;

		if (managers.TryGetValue(GetManagerName(), out Variant gameManagerValue))
		{
			var gameManagerData = gameManagerValue.AsGodotDictionary<string, Variant>();
			gameManagerData["currentBase"] = currentBase.Save();
			gameManagerData["currentBaseFunds"] = currentBaseFunds;
			managers[GetManagerName()] = gameManagerData;
		}

		globeData["managers"] = managers;
	}

	private static void UpdateMissionStatusInSavedData(
		Godot.Collections.Dictionary<string, Variant> root,
		MissionCellDefinition mission
	)
	{
		if (!root.TryGetValue("managers", out var m)) return;
		var managers = m.AsGodotDictionary<string, Variant>();
		if (!managers.TryGetValue("GlobeMissionManager", out var mm)) return;
		var missionData = mm.AsGodotDictionary<string, Variant>();
		if (!missionData.TryGetValue("activeMissions", out var am)) return;
		var missions = am.AsGodotDictionary<string, Variant>();
		if (!missions.TryGetValue(mission.cellIndex.ToString(), out var savedMission)) return;

		var savedMissionData = savedMission.AsGodotDictionary<string, Variant>();
		savedMissionData["missionStatus"] = (int)mission.missionStatus;
	}

	#endregion

	#region Data Handling

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			["mapSize"] = mapSize,
			["unitCounts"] = unitCounts,
			["currentScene"] = (int)currentScene,
			["currentBase"] = currentBase?.Save(),
			["currentBaseFunds"] = currentBaseFunds,
		};
	}

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data == null) return Task.CompletedTask;
		if (data.ContainsKey("mapSize")) mapSize = (Vector2I)data["mapSize"];
		if (data.ContainsKey("unitCounts")) unitCounts = (Vector2I)data["unitCounts"];
		if (data.ContainsKey("currentScene")) currentScene = (GameScene)(int)data["currentScene"];
		if (data.ContainsKey("currentBaseFunds")) currentBaseFunds = data["currentBaseFunds"].AsInt32();
		if (data.ContainsKey("currentBase") && data["currentBase"].VariantType != Variant.Type.Nil)
		{
			currentBase = new TeamBaseCellDefinition(-1, "", Enums.UnitTeam.None, null);
			currentBase.Load(data["currentBase"].AsGodotDictionary<string, Variant>());
		}
		return Task.CompletedTask;
	}

	#endregion
}
