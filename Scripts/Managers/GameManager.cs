using System;
using System.Collections.Generic;
using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.UI;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

public partial class GameManager : Manager<GameManager>
{
	public const int UnitHiringCost = 25000;

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
	private GlobeTeamHolder currentBaseTeamContext;
	private long _currentBaseFunds;
	public long currentBaseFunds
	{
		get => _currentBaseFunds;
		set
		{
			_currentBaseFunds = value;
			if (currentBaseTeamContext != null &&
			    GodotObject.IsInstanceValid(currentBaseTeamContext))
			{
				currentBaseTeamContext.funds = value;
			}
		}
	}
	public PackedScene unitScene;
	private Godot.Collections.Array<
		Godot.Collections.Dictionary<string, Variant>> _pendingBattlePlayerUnits;
	private MissionBattleResult _pendingBattleResult;
	private bool _battleEnding;
	private bool _isQuickBattleSession;
	private Godot.Collections.Dictionary<string, Variant>
		_savedBattleDeploymentInventory;

	public bool HasPendingBattlePlayerUnits => _pendingBattlePlayerUnits != null;
	public bool IsQuickBattle => _isQuickBattleSession;

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
			if (!GodotObject.IsInstanceValid(m)) continue;
			try
			{
				m.Deinitialize();
			}
			catch (Exception exception)
			{
				GD.PrintErr(
					$"Could not deinitialize {m.GetManagerName()}: {exception.Message}");
			}
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
		SetCurrentTeamResearchState(null, null);

		return await ChangeSceneAsync(scene, false);
	}

	public async Task<bool> TryChangeScene(GameScene sceneName, bool saveManagerData = true, bool loadSceneData = true)
	{
		if (!scenePaths.ContainsKey(sceneName)) return false;

		var sm = SavesManager.Instance;
		if (sceneName == GameScene.BattleScene)
		{
			bool hasCampaignReturnState = sm != null &&
			                              sm.TryGetSessionData(
				                              "GlobeState",
				                              out _);
			_isQuickBattleSession = currentScene != GameScene.GlobeScene ||
			                        currentMission == null ||
			                        !hasCampaignReturnState;
			if (_isQuickBattleSession) currentMission = null;
		}
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

		bool changed = await ChangeSceneAsync(sceneName, true);
		if (!changed)
		{
			SavesManager.PendingSaveData = null;
			SavesManager.LoadFromAutosave = false;
		}
		return changed;
	}

	/// <summary>
	/// The core transition worker for the Autoload.
	/// </summary>
	public async Task<bool> ChangeSceneAsync(GameScene scene, bool loadingData)
	{
		if (!scenePaths.TryGetValue(scene, out string scenePath)) return false;
		PackedScene packedScene = ResourceLoader.Load<PackedScene>(scenePath);
		Node nextScene = packedScene?.Instantiate();
		if (nextScene == null) return false;

		loadingState = LoadingState.CHANGINGSCENES;
		loadingPercent = 0;
		loadingManagerName = "Scene Transition";
		UIManager.Instance?.ShowLoadingScreen();

		CleanupManagers();
		GameScene previousScene = currentScene;
		currentScene = scene;
		Error err = GetTree().ChangeSceneToNode(nextScene);
		if (err != Error.Ok)
		{
			currentScene = previousScene;
			if (GodotObject.IsInstanceValid(nextScene)) nextScene.QueueFree();
			loadingState = LoadingState.NONE;
			loadingManagerName = string.Empty;
			if (UIManager.Instance != null &&
			    GodotObject.IsInstanceValid(UIManager.Instance))
				await UIManager.Instance.HideLoadingScreen();
			return false;
		}

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
		return true;
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
		SavesManager.PendingSaveData = null;

		// Modal windows can pause the scene tree, so make loading-screen removal
		// part of the transition contract instead of waiting for its next _Process.
		if (UIManager.Instance != null &&
		    GodotObject.IsInstanceValid(UIManager.Instance))
		{
			await UIManager.Instance.HideLoadingScreen();
		}

		EmitSignal(SignalName.CoreManagersLoaded);
	}

	#endregion

	#region Game Logic & Rules

	public void SetCurrentBase(
		TeamBaseCellDefinition baseDefinition,
		GlobeTeamHolder sourceTeam)
	{
		if (baseDefinition == null)
			throw new ArgumentNullException(nameof(baseDefinition));
		if (sourceTeam == null || !GodotObject.IsInstanceValid(sourceTeam))
			throw new ArgumentException(
				"The selected base must have a valid source team.",
				nameof(sourceTeam));

		currentBase = baseDefinition;
		currentBaseFunds = sourceTeam.funds;
		AttachCurrentBaseTeamContext(new GlobeTeamHolder(
			sourceTeam.Team,
			new List<TeamBaseCellDefinition> { currentBase },
			currentBaseFunds));
	}

	private void AttachCurrentBaseTeamContext(GlobeTeamHolder teamContext)
	{
		if (teamContext == null)
			throw new ArgumentNullException(nameof(teamContext));

		if (currentBaseTeamContext != null &&
		    GodotObject.IsInstanceValid(currentBaseTeamContext) &&
		    currentBaseTeamContext.GetParent() == this)
		{
			currentBaseTeamContext.QueueFree();
		}

		currentBaseTeamContext = teamContext;
		currentBaseTeamContext.funds = currentBaseFunds;
		AddChild(currentBaseTeamContext);
		currentBase.SetParentTeamHolder(currentBaseTeamContext);
	}

	public bool CanHireUnits(int count = 1)
	{
		if (count <= 0 || currentBase == null || unitScene == null) return false;
		if (currentBase.GetStationedGridObjects().Count + count >
			currentBase.MaxStationedUnits)
			return false;

		long totalCost = (long)UnitHiringCost * count;
		return currentBaseFunds >= totalCost;
	}

	public bool TryHireUnits(int count = 1)
	{
		if (!CanHireUnits(count) || !TryAddHiredUnitsWithoutPurchase(count))
			return false;

		currentBaseFunds -= UnitHiringCost * count;
		currentBase.RecordBaseExpenditure(
			(long)UnitHiringCost * count,
			"Unit recruitment");
		if (!SyncCurrentBaseToGlobeState())
			GD.PrintErr("Units were hired locally, but the globe transition state could not be updated.");
		return true;
	}

	internal bool TryAddHiredUnitsWithoutPurchase(int count)
	{
		if (count <= 0 || currentBase == null || unitScene == null) return false;

		var addedUnits = new Godot.Collections.Array<GridObject>();
		for (int i = 0; i < count; i++)
		{
			GridObject newUnit = unitScene.Instantiate<GridObject>();
			newUnit.Name = UnitNameGenerator.Generate();

			if (currentBase.TryAddStationedGridObject(newUnit))
			{
				addedUnits.Add(newUnit);
				continue;
			}

			newUnit.QueueFree();
			foreach (GridObject addedUnit in addedUnits)
			{
				currentBase.TryRemoveStationedGridObject(addedUnit);
				addedUnit.QueueFree();
			}
			return false;
		}

		return true;
	}

	public void PrepareBattleLoadout(Craft craft)
	{
		_pendingBattlePlayerUnits = new Godot.Collections.Array<
			Godot.Collections.Dictionary<string, Variant>>();
		if (craft != null)
		{
			foreach (GridObject unit in craft.GetStationedGridObjects())
			{
				if (unit == null || !GodotObject.IsInstanceValid(unit)) continue;

				var unitData = unit.Save();
				string scenePath = unitData.TryGetValue(
					"Filename",
					out Variant filenameValue)
					? filenameValue.AsString()
					: string.Empty;
				if (string.IsNullOrWhiteSpace(scenePath))
				{
					scenePath = unit.SceneFilePath;
					if (string.IsNullOrWhiteSpace(scenePath))
						scenePath = unitScene?.ResourcePath;
				}

				if (string.IsNullOrWhiteSpace(scenePath))
				{
					GD.PrintErr($"Could not prepare unit {unit.Name}: its scene path is missing.");
					continue;
				}

				unitData["Filename"] = scenePath;
				_pendingBattlePlayerUnits.Add(unitData.Duplicate(true));
			}
		}

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

	public bool RestoreBattleDeploymentInventory(InventoryGrid inventory)
	{
		if (inventory == null || _savedBattleDeploymentInventory == null)
			return false;
		inventory.LoadContents(_savedBattleDeploymentInventory);
		_savedBattleDeploymentInventory = null;
		return true;
	}

	public void CheckGameState(Turn currentTurn)
	{
		if (_pendingBattleResult != null || _battleEnding ||
		    GridObjectManager.Instance == null)
			return;

		GridObjectTeamHolder playerHolder = GridObjectManager.Instance
			.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		GridObjectTeamHolder enemyHolder = GridObjectManager.Instance
			.GetGridObjectTeamHolder(Enums.UnitTeam.Enemy);
		if (playerHolder == null || enemyHolder == null) return;

		bool playerDefeated = GetGridObjectCount(
			playerHolder,
			Enums.GridObjectState.Active) == 0;
		bool enemyDefeated = GetGridObjectCount(
			enemyHolder,
			Enums.GridObjectState.Active) == 0;
		if (!playerDefeated && !enemyDefeated) return;

		Enums.MissionStatus outcome = playerDefeated
			? Enums.MissionStatus.Failed
			: Enums.MissionStatus.Successful;
		SetCurrentMissionOutcome(outcome);
		EndGame(outcome);
	}

	private async void EndGame(Enums.MissionStatus outcome)
	{
		try
		{
			await PresentBattleEnd(outcome);
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				$"Could not show the mission report: {exception.Message}\n{exception.StackTrace}");
		}
	}

	public async Task EndBattleAndReturnToGlobe()
	{
		if (_pendingBattleResult == null)
		{
			Enums.MissionStatus outcome = GetCurrentMissionOutcome(currentMission);
			if (outcome != Enums.MissionStatus.None)
				await PresentBattleEnd(outcome);
			return;
		}

		await ConfirmBattleEnd(_pendingBattleResult);
	}

	public async Task<bool> ConfirmBattleEnd(MissionBattleResult result)
	{
		if (_battleEnding || result == null ||
		    !ReferenceEquals(result, _pendingBattleResult) ||
		    result.IsOverCapacity)
			return false;

		_battleEnding = true;

		try
		{
			if (result.IsQuickBattle)
			{
				bool changed = await ChangeSceneAsync(
					GameScene.MainMenu,
					false);
				if (!changed) return false;
				_pendingBattleResult = null;
				currentMission = null;
				return true;
			}

			return await CommitMissionBattleResult(result);
		}
		catch (Exception exception)
		{
			GD.PrintErr(
				$"Could not finish the mission: {exception.Message}\n{exception.StackTrace}");
			return false;
		}
		finally
		{
			_battleEnding = false;
		}
	}

	public async Task<bool> AbandonCurrentMission()
	{
		if (_battleEnding || _pendingBattleResult != null || IsQuickBattle ||
		    currentScene != GameScene.BattleScene ||
		    currentMission == null)
			return false;

		SetCurrentMissionOutcome(Enums.MissionStatus.Aborted);
		return await PresentBattleEnd(Enums.MissionStatus.Aborted);
	}

	private async Task<bool> PresentBattleEnd(Enums.MissionStatus outcome)
	{
		if (_battleEnding || _pendingBattleResult != null) return false;
		BattleEndUI battleEndUI = UIManager.Instance?.GetWindow<BattleEndUI>();
		if (battleEndUI == null)
		{
			GD.PrintErr("BattleEndUI is not available in the battle scene.");
			return false;
		}

		MissionBattleResult result = CreateBattleResult(outcome);
		_pendingBattleResult = result;
		try
		{
			await battleEndUI.ShowResult(result);
			return true;
		}
		catch
		{
			_pendingBattleResult = null;
			throw;
		}
	}

	private MissionBattleResult CreateBattleResult(
		Enums.MissionStatus outcome)
	{
		bool isQuickBattle = IsQuickBattle;
		MouseHeldInventoryUI heldInventoryUI =
			UIManager.Instance?.mouseHeldInventoryUI;
		heldInventoryUI?.TryReturnHeldItem();
		var recovery = new MissionRecoveryResult();
		if (!isQuickBattle && currentMission?.mission != null &&
		    currentMission.onRouteCraft != null)
		{
			StartingEuipmentUI equipmentUI = UIManager.Instance?
				.GetWindow<StartingEuipmentUI>();
			recovery = MissionRecoveryResolver.Resolve(
				currentMission.mission,
				outcome,
				equipmentUI?.GetInventoryGrid(Enums.InventoryType.Ground),
				heldInventoryUI?.InventoryGrid);
		}

		GridObjectTeamHolder enemyHolder = GridObjectManager.Instance?
			.GetGridObjectTeamHolder(Enums.UnitTeam.Enemy);
		GridObjectTeamHolder playerHolder = GridObjectManager.Instance?
			.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		int enemiesKilled = GetGridObjectCount(
			enemyHolder,
			Enums.GridObjectState.Inactive);
		int unitsLost = isQuickBattle
			? GetGridObjectCount(playerHolder, Enums.GridObjectState.Inactive)
			: Math.Max(
				0,
				GetGridObjectCount(playerHolder, Enums.GridObjectState.Active) +
				GetGridObjectCount(playerHolder, Enums.GridObjectState.Inactive) -
				recovery.RecoveredUnits.Count);

		MissionScoreBreakdown score = !isQuickBattle && currentMission?.mission != null
			? currentMission.mission.CalculateScore(
				enemiesKilled,
				unitsLost,
				outcome)
			: MissionBase.CalculateDefaultScore(
				enemiesKilled,
				unitsLost,
				outcome);
		long capacity = isQuickBattle
			? long.MaxValue
			: GetRecoveryWeightCapacity();
		string missionName = isQuickBattle
			? "Quick Battle"
			: currentMission?.mission?.missionName;

		return new MissionBattleResult(
			outcome,
			score,
			recovery,
			isQuickBattle,
			capacity,
			missionName);
	}

	private async Task<bool> CommitMissionBattleResult(
		MissionBattleResult result)
	{
		MissionCellDefinition mission = currentMission;
		if (mission == null || SavesManager.Instance == null) return false;
		var battleState = SavesManager.Instance.PackageFullState()
			.Duplicate(true);

		if (!SavesManager.Instance.TryGetSessionData(
			    "GlobeState",
			    out Variant globeStateValue) ||
		    globeStateValue.VariantType != Variant.Type.Dictionary)
			return false;

		var globeData = globeStateValue
			.AsGodotDictionary<string, Variant>()
			.Duplicate(true);
		mission.SetBattleResult(result);
		bool payloadUpdated = UpdateMissionCraftPayloadInSavedData(
			globeData,
			mission,
			result.Recovery);
		bool missionUpdated = payloadUpdated &&
		                      UpdateMissionStatusInSavedData(globeData, mission);
		if (!payloadUpdated || !missionUpdated)
		{
			GD.PrintErr("The mission result could not be written to the globe state.");
			return false;
		}

		SavesManager.PendingSaveData = globeData;
		SavesManager.LoadFromAutosave = false;
		bool changed;
		try
		{
			changed = await ChangeSceneAsync(GameScene.GlobeScene, true);
		}
		catch (Exception exception)
		{
			SavesManager.PendingSaveData = null;
			GD.PrintErr(
				$"The globe scene could not finish loading: {exception.Message}");
			await RestoreBattleReportAfterFailedCommit(
				result,
				battleState,
				mission);
			return false;
		}
		if (!changed)
		{
			SavesManager.PendingSaveData = null;
			return false;
		}

		SavesManager.Instance.ConsumeSceneState("GlobeState");
		currentMission = null;
		_pendingBattleResult = null;
		return true;
	}

	private async Task<bool> RestoreBattleReportAfterFailedCommit(
		MissionBattleResult result,
		Godot.Collections.Dictionary<string, Variant> battleState,
		MissionCellDefinition mission)
	{
		BattleEndUI existingReport = UIManager.Instance?
			.GetWindow<BattleEndUI>();
		if (existingReport != null &&
		    GodotObject.IsInstanceValid(existingReport) &&
		    existingReport.IsInsideTree())
		{
			currentScene = GameScene.BattleScene;
			currentMission = mission;
			_pendingBattleResult = result;
			SetCurrentMissionOutcome(result.Outcome);
			return true;
		}

		if (battleState == null) return false;
		SavesManager.PendingSaveData = battleState;
		SavesManager.LoadFromAutosave = false;
		try
		{
			if (!await ChangeSceneAsync(GameScene.BattleScene, true))
			{
				SavesManager.PendingSaveData = null;
				return false;
			}

			_pendingBattleResult = result;
			SetCurrentMissionOutcome(result.Outcome);
			BattleEndUI restoredReport = UIManager.Instance?
				.GetWindow<BattleEndUI>();
			if (restoredReport == null) return false;
			await restoredReport.ShowResult(result);
			return true;
		}
		catch (Exception exception)
		{
			SavesManager.PendingSaveData = null;
			GD.PrintErr(
				$"The battle report could not be restored: {exception.Message}");
			return false;
		}
	}

	private void SetCurrentMissionOutcome(Enums.MissionStatus outcome)
	{
		if (currentMission == null || IsQuickBattle) return;
		currentMission.missionStatus &= ~(
			Enums.MissionStatus.OnRoute |
			Enums.MissionStatus.Successful |
			Enums.MissionStatus.Failed |
			Enums.MissionStatus.Timeout |
			Enums.MissionStatus.Aborted);
		currentMission.missionStatus |= Enums.MissionStatus.Visited | outcome;
	}

	private long GetRecoveryWeightCapacity()
	{
		Craft returningCraft = currentMission?.onRouteCraft;
		if (returningCraft != null) return returningCraft.ItemWeightCapacity;
		return 0;
	}

	private static int GetGridObjectCount(
		GridObjectTeamHolder holder,
		Enums.GridObjectState state)
	{
		if (holder?.GridObjects == null ||
		    !holder.GridObjects.TryGetValue(state, out List<GridObject> objects) ||
		    objects == null)
			return 0;
		return objects.Count;
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

	private static bool UpdateMissionStatusInSavedData(
		Godot.Collections.Dictionary<string, Variant> root,
		MissionCellDefinition mission
	)
	{
		if (root == null || mission == null ||
		    !root.TryGetValue("managers", out var m)) return false;
		var managers = m.AsGodotDictionary<string, Variant>();
		if (!managers.TryGetValue("GlobeMissionManager", out var mm)) return false;
		var missionData = mm.AsGodotDictionary<string, Variant>();
		if (!missionData.TryGetValue("activeMissions", out var am)) return false;
		var missions = am.AsGodotDictionary<string, Variant>();
		if (!missions.TryGetValue(
			    mission.cellIndex.ToString(),
			    out var savedMission)) return false;

		var savedMissionData = savedMission.AsGodotDictionary<string, Variant>();
		savedMissionData["missionStatus"] = (int)mission.missionStatus;
		savedMissionData["battleResult"] = mission.SaveBattleResult();
		missions[mission.cellIndex.ToString()] = savedMissionData;
		missionData["activeMissions"] = missions;
		managers["GlobeMissionManager"] = missionData;
		root["managers"] = managers;
		return true;
	}

	private static Enums.MissionStatus GetCurrentMissionOutcome(
		MissionCellDefinition mission)
	{
		if (mission == null) return Enums.MissionStatus.None;
		if (mission.missionStatus.HasFlag(Enums.MissionStatus.Successful))
			return Enums.MissionStatus.Successful;
		if (mission.missionStatus.HasFlag(Enums.MissionStatus.Aborted))
			return Enums.MissionStatus.Aborted;
		if (mission.missionStatus.HasFlag(Enums.MissionStatus.Failed))
			return Enums.MissionStatus.Failed;
		if (mission.missionStatus.HasFlag(Enums.MissionStatus.Timeout))
			return Enums.MissionStatus.Timeout;
		return Enums.MissionStatus.None;
	}

	private static bool UpdateMissionCraftPayloadInSavedData(
		Godot.Collections.Dictionary<string, Variant> root,
		MissionCellDefinition mission,
		MissionRecoveryResult recovery)
	{
		Craft missionCraft = mission?.onRouteCraft;
		if (recovery == null || root == null) return false;
		if (missionCraft == null)
			return recovery.RecoveredUnits.Count == 0 &&
			       recovery.RecoveredItems.Count == 0;
		if (!root.TryGetValue("managers", out Variant managersValue)) return false;

		var managers = managersValue.AsGodotDictionary<string, Variant>();
		if (!managers.TryGetValue(
			    "GlobeTeamManager",
			    out Variant teamManagerValue))
			return false;

		var teamManagerData =
			teamManagerValue.AsGodotDictionary<string, Variant>();
		if (!teamManagerData.TryGetValue("teamData", out Variant teamDataValue))
			return false;

		var teamData = teamDataValue.AsGodotDictionary<string, Variant>();
		if (!teamData.TryGetValue(
			    ((int)Enums.UnitTeam.Player).ToString(),
			    out Variant playerTeamValue))
			return false;

		var playerTeam = playerTeamValue.AsGodotDictionary<string, Variant>();
		if (!playerTeam.TryGetValue("bases", out Variant basesValue)) return false;

		var bases = basesValue.AsGodotDictionary<string, Variant>();
		if (!bases.TryGetValue(
			    missionCraft.HomeBaseIndex.ToString(),
			    out Variant homeBaseValue))
			return false;

		var homeBase = homeBaseValue.AsGodotDictionary<string, Variant>();
		if (!homeBase.TryGetValue("crafts", out Variant craftsValue)) return false;

		var crafts = craftsValue.AsGodotArray<
			Godot.Collections.Dictionary<string, Variant>>();
		for (int i = 0; i < crafts.Count; i++)
		{
			var craftData = crafts[i];
			if (!craftData.TryGetValue("index", out Variant indexValue) ||
			    indexValue.AsInt32() != missionCraft.Index)
				continue;

			craftData["stationedUnits"] = recovery.RecoveredUnits;
			craftData["stationedItems"] = recovery.RecoveredItems;
			crafts[i] = craftData;
			homeBase["crafts"] = crafts;
			bases[missionCraft.HomeBaseIndex.ToString()] = homeBase;
			playerTeam["bases"] = bases;
			teamData[((int)Enums.UnitTeam.Player).ToString()] = playerTeam;
			teamManagerData["teamData"] = teamData;
				managers["GlobeTeamManager"] = teamManagerData;
				root["managers"] = managers;
				return true;
		}

		GD.PrintErr(
			$"Could not apply mission recovery to craft {missionCraft.Index}.");
		return false;
	}

	#endregion

	#region Data Handling

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>
		{
			["mapSize"] = mapSize,
			["unitCounts"] = unitCounts,
			["currentScene"] = (int)currentScene,
			["isQuickBattle"] = _isQuickBattleSession,
			["currentBase"] = currentBase?.Save(),
			["currentBaseFunds"] = currentBaseFunds,
			["currentTeamUnlockedItemIds"] = SaveCurrentTeamUnlockedItems(),
			["currentTeamCompletedResearchIds"] = SaveCurrentTeamCompletedResearch(),
		};

		if (currentScene == GameScene.BattleScene &&
		    !_isQuickBattleSession && currentMission != null &&
		    SavesManager.Instance != null &&
		    SavesManager.Instance.TryGetSessionData(
			    "GlobeState",
			    out Variant globeState))
		{
			data["battleMissionCellIndex"] = currentMission.cellIndex;
			data["battleReturnGlobeState"] = globeState;
		}
		if (currentScene == GameScene.BattleScene)
		{
			InventoryGrid deploymentInventory = UIManager.Instance?
				.GetWindow<StartingEuipmentUI>()?
				.GetInventoryGrid(Enums.InventoryType.Ground);
			if (deploymentInventory != null)
				data["battleDeploymentInventory"] =
					deploymentInventory.SaveContents();
		}
		if (_pendingBattlePlayerUnits != null)
			data["pendingBattlePlayerUnits"] =
				_pendingBattlePlayerUnits.Duplicate(true);

		return data;
	}

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data == null) return Task.CompletedTask;
		GameScene targetScene = currentScene;
		_savedBattleDeploymentInventory = null;
		if (targetScene == GameScene.BattleScene &&
		    data.TryGetValue(
			    "pendingBattlePlayerUnits",
			    out Variant pendingUnitsValue) &&
		    pendingUnitsValue.VariantType == Variant.Type.Array)
		{
			_pendingBattlePlayerUnits = pendingUnitsValue
				.AsGodotArray<
					Godot.Collections.Dictionary<string, Variant>>()
				.Duplicate(true);
		}
		if (data.ContainsKey("mapSize")) mapSize = (Vector2I)data["mapSize"];
		if (data.ContainsKey("unitCounts")) unitCounts = (Vector2I)data["unitCounts"];
		if (data.ContainsKey("currentScene")) currentScene = (GameScene)(int)data["currentScene"];
		bool savedBattleState = currentScene == GameScene.BattleScene;
		bool hasSavedBattleMode = data.ContainsKey("isQuickBattle");
		if (hasSavedBattleMode)
			_isQuickBattleSession = data["isQuickBattle"].AsBool();

		if (targetScene != GameScene.BattleScene)
		{
			currentMission = null;
			_isQuickBattleSession = false;
		}
		else if (savedBattleState)
		{
			if (data.TryGetValue(
				    "battleDeploymentInventory",
				    out Variant deploymentInventoryValue) &&
			    deploymentInventoryValue.VariantType == Variant.Type.Dictionary)
			{
				_savedBattleDeploymentInventory = deploymentInventoryValue
					.AsGodotDictionary<string, Variant>();
			}

			Godot.Collections.Dictionary<string, Variant> battleReturnState = null;
			if (data.TryGetValue(
				    "battleReturnGlobeState",
				    out Variant globeStateValue) &&
			    globeStateValue.VariantType == Variant.Type.Dictionary)
			{
				battleReturnState = globeStateValue
					.AsGodotDictionary<string, Variant>();
				SavesManager.Instance?.SetSessionData(
					"GlobeState",
					globeStateValue);
			}

			currentMission = null;
			if (battleReturnState != null &&
			    data.TryGetValue(
				    "battleMissionCellIndex",
				    out Variant missionIndexValue) &&
			    TryGetSavedMissionDefinition(
				    battleReturnState,
				    missionIndexValue.AsInt32(),
				    out var savedMissionDefinition))
			{
				currentMission = RestoreBattleMission(savedMissionDefinition);
			}

			if (!_isQuickBattleSession && currentMission == null)
			{
				GD.PrintErr(
					"The saved campaign mission could not be restored. Loading as a quick battle.");
				_isQuickBattleSession = true;
			}
			else if (!hasSavedBattleMode)
				_isQuickBattleSession = currentMission == null;
		}
		if (data.ContainsKey("currentBaseFunds")) currentBaseFunds = data["currentBaseFunds"].AsInt64();
		LoadCurrentTeamUnlockedItems(data);
		LoadCurrentTeamCompletedResearch(data);
		if (data.ContainsKey("currentBase") && data["currentBase"].VariantType != Variant.Type.Nil)
		{
			var baseData =
				data["currentBase"].AsGodotDictionary<string, Variant>();
			Enums.UnitTeam team = baseData.TryGetValue(
				"teamAffiliation",
				out Variant savedTeam)
				? (Enums.UnitTeam)savedTeam.AsInt32()
				: Enums.UnitTeam.None;
			var teamContext = new GlobeTeamHolder(
				team,
				new List<TeamBaseCellDefinition>(),
				currentBaseFunds);
			currentBase = new TeamBaseCellDefinition(
				-1,
				"",
				team,
				null,
				teamContext);
			teamContext.Bases.Add(currentBase);
			currentBase.Load(baseData);
			AttachCurrentBaseTeamContext(teamContext);
		}
		return Task.CompletedTask;
	}

	private static bool TryGetSavedMissionDefinition(
		Godot.Collections.Dictionary<string, Variant> globeState,
		int cellIndex,
		out Godot.Collections.Dictionary<string, Variant> missionDefinition)
	{
		missionDefinition = null;
		if (globeState == null ||
		    !globeState.TryGetValue("managers", out Variant managersValue))
			return false;

		var managers = managersValue.AsGodotDictionary<string, Variant>();
		if (!managers.TryGetValue(
			    "GlobeMissionManager",
			    out Variant managerValue))
			return false;

		var managerData = managerValue.AsGodotDictionary<string, Variant>();
		if (!managerData.TryGetValue(
			    "activeMissions",
			    out Variant missionsValue))
			return false;

		var missions = missionsValue.AsGodotDictionary<string, Variant>();
		if (!missions.TryGetValue(cellIndex.ToString(), out Variant missionValue) ||
		    missionValue.VariantType != Variant.Type.Dictionary)
			return false;

		missionDefinition = missionValue
			.AsGodotDictionary<string, Variant>();
		return true;
	}

	private static MissionCellDefinition RestoreBattleMission(
		Godot.Collections.Dictionary<string, Variant> missionDefinitionData)
	{
		try
		{
			var missionData = missionDefinitionData["missionData"]
				.AsGodotDictionary<string, Variant>();
			string missionClass = missionDefinitionData["missionClass"].AsString();
			int cellIndex = missionDefinitionData["cellIndex"].AsInt32();
			Enums.MissionType missionType =
				(Enums.MissionType)missionData["type"].AsInt32();
			Enums.MissionRecoveryType recoveryType = missionData.TryGetValue(
				"recoveryType",
				out Variant recoveryValue)
				? (Enums.MissionRecoveryType)recoveryValue.AsInt32()
				: Enums.MissionRecoveryType.FullFieldOnSuccess;
			string missionName = missionData.TryGetValue(
				"name",
				out Variant nameValue)
				? nameValue.AsString()
				: "Mission";
			string missionDescription = missionData.TryGetValue(
				"description",
				out Variant descriptionValue)
				? descriptionValue.AsString()
				: string.Empty;
			int difficulty = missionData.TryGetValue(
				"difficulty",
				out Variant difficultyValue)
				? difficultyValue.AsInt32()
				: 1;
			int enemyCount = missionData["enemyCount"].AsInt32();

			MissionBase mission = missionClass == nameof(EliminateMission)
				? new EliminateMission(
					missionName,
					missionDescription,
					missionType,
					difficulty,
					enemyCount,
					cellIndex,
					recoveryType)
				: null;
			if (mission == null) return null;
			mission.RestoreScoring(missionData);

			Craft onRouteCraft = null;
			if (missionDefinitionData.TryGetValue(
				    "onRouteCraft",
				    out Variant craftValue) &&
			    craftValue.VariantType == Variant.Type.Dictionary)
			{
				var craftData = craftValue.AsGodotDictionary<string, Variant>();
				if (craftData.Count > 0)
				{
					onRouteCraft = new Craft();
					onRouteCraft.Load(craftData);
				}
			}

			Enums.MissionStatus status = missionDefinitionData.TryGetValue(
				"missionStatus",
				out Variant statusValue)
				? (Enums.MissionStatus)statusValue.AsInt32()
				: Enums.MissionStatus.None;
			string definitionName = missionDefinitionData.TryGetValue(
				"definitionName",
				out Variant definitionNameValue)
				? definitionNameValue.AsString()
				: missionName;
			int alienOperationId = missionDefinitionData.TryGetValue(
				"alienOperationId",
				out Variant operationValue)
				? operationValue.AsInt32()
				: -1;
			int timeoutTime = missionDefinitionData.TryGetValue(
				"timeoutTime",
				out Variant timeoutValue)
				? timeoutValue.AsInt32()
				: 12;
			int timeLeft = missionDefinitionData.TryGetValue(
				"timeLeft",
				out Variant timeLeftValue)
				? timeLeftValue.AsInt32()
				: timeoutTime;

			var definition = new MissionCellDefinition(
				cellIndex,
				definitionName,
				mission,
				null,
				status,
				onRouteCraft,
				alienOperationId);
			definition.RestoreTimeoutState(timeoutTime, timeLeft);
			definition.RestoreBattleResult(missionDefinitionData);
			return definition;
		}
		catch (Exception exception)
		{
			GD.PrintErr($"Could not restore the battle mission: {exception.Message}");
			return null;
		}
	}

	#endregion
}
