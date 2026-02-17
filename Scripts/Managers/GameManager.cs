using System;
using System.Collections.Generic;
using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

/// <summary>
/// The central orchestrator for the game's lifecycle, managing scene transitions, 
/// state persistence (saving/loading), and the initialization/execution of all sub-managers.
/// It acts as the root of the manager hierarchy and ensures data consistency across scene swaps.
/// </summary>
[GlobalClass]
public partial class GameManager : Manager<GameManager>
{

	#region variables / Properties


	/// <summary> List of sub-managers that this GameManager is responsible for initializing and executing. </summary>
	[Export] public Array<ManagerBase> managers = new();

	/// <summary> Mapping of GameScene enum values to their respective scene file paths on disk. </summary>
	static Godot.Collections.Dictionary<GameScene, string> scenePaths = new()
	{
		{ GameScene.BattleScene, "res://Scenes/GameScenes/BattleScene.tscn" },
		{ GameScene.GlobeScene,  "res://Scenes/GameScenes/GlobeScene.tscn" },
		{ GameScene.MainMenu,  "res://Scenes/GameScenes/MainMenuScene.tscn" }
	};
	
	[Export] protected string saveDir = "user://saves/";
	
	public Vector2I mapSize = new Vector2I(1, 1);
	public Vector2I unitCounts = new Vector2I(2, 2);
	
	public enum GameScene
	{
		NONE,
		MainMenu,
		BattleScene,
		GlobeScene
	}
	
	public enum LoadingState
	{
		NONE,
		CHANGINGSCENES,
		SETTINGUPMANAGERS,
		EXECUTINGMANAGERS
	}
	
	[Export] public GameScene currentScene;

	private string currentSavename = "new SaveGame";
	private const string SaveExt = ".sav";
	private const string AutosaveName = "autosave";

	/// Temporary storage for game state data during a scene transition when not saving to disk. </summary>
	private static Godot.Collections.Dictionary<string, Variant> _pendingSaveData = null;
	
	private static bool _loadFromAutosave = false;
	
	private static string _pendingSaveName = "";

	/// <summary>
	/// value from 0 to 1 representing current load progress
	/// </summary>
	public float loadingPercent = 0;
	public LoadingState loadingState = LoadingState.NONE;	

	#endregion


	#region Signals
	
	/// Emitted when the list of available save games changes (e.g., after a save or delete). </summary>
	[Signal]
	public delegate void GameSavesChangedEventHandler();

	/// <summary> Emitted when all core managers have finished setup and execution. </summary>
	[Signal]
	public delegate void CoreManagersLoadedEventHandler();
	#endregion

	#region Functions
	public override string GetManagerName() => "GameManager";

	/// <summary>
	/// Entry point for the GameManager. handles the initial state setup, 
	/// determining whether to load from an autosave, pending data, or start a fresh session.
	/// </summary>
	public override async void _Ready()
	{
		base._Ready();

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (_loadFromAutosave)
		{
			// Load state from the physical autosave file if flagged.
			_loadFromAutosave = false;
			await LoadAutosaveInternal();
		}
		else if (_pendingSaveData != null)
		{
			// Load state from static pending data when transitioning between scenes 
			// without saving to disk.
			currentSavename = _pendingSaveName;
			var data = _pendingSaveData;
			_pendingSaveData = null;
			_pendingSaveName = "";
			await PerformInternalLoadSequence(data);
		}
		else
		{
			// Standard fresh setup of the current scene and managers.
			await SetupCall(false);
		}
	}

	/// <summary>
	/// Opens and reads the autosave file from the save directory, 
	/// then initiates the internal loading sequence with its contents.
	/// </summary>
	private async Task LoadAutosaveInternal()
	{
		string fullName = AutosaveName + SaveExt;
		var path = saveDir.PathJoin(fullName);

		if (FileAccess.FileExists(path))
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			if (file != null)
			{
				try
				{
					var root = file.GetVar().AsGodotDictionary<string, Variant>();
					await PerformInternalLoadSequence(root);
					return;
				}
				catch (Exception e)
				{
					GD.PrintErr($"Failed to load autosave: {e.Message}");
				}
			}
		}
		// Fallback if autosave fails or doesn't exist.
		await SetupCall(false);
	}

	/// <summary>
	/// manages the initial setup of all child managers. 
	/// </summary>
	protected override async Task _Setup(bool loadingData)
	{
		loadingState = LoadingState.SETTINGUPMANAGERS;
		loadingPercent = 0;

		managers ??= new Array<ManagerBase>();
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase m && !managers.Contains(m))
			{
				managers.Add(m);
			}
		}

		int coreManagersCount = 0;
		foreach (var m in managers) if (m.includeInLoadingCalculation) coreManagersCount++;
		int totalSteps = coreManagersCount * 2;
		int completedSteps = 0;

		// Initialize each sub-manager.
		foreach (ManagerBase manager in managers)
		{
			if (DebugMode)
			{
				GD.Print($"Setting up manager: {manager.GetManagerName()}");
			}
			await manager.SetupCall(loadingData);
			if (manager.includeInLoadingCalculation)
			{
				completedSteps++;
				loadingPercent = totalSteps > 0 ? (float)completedSteps / totalSteps : 1.0f;
			}
			// Yield to main thread to allow UI to update
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		await ExecuteCall(loadingData);
	}

	/// <summary>
	/// Runs the execution phase for all  sub-managers, 
	/// </summary>
	protected override async Task _Execute(bool loadingData)
	{
		loadingState = LoadingState.EXECUTINGMANAGERS;

		int coreManagersCount = 0;
		foreach (var m in managers) if (m.includeInLoadingCalculation) coreManagersCount++;
		int totalSteps = coreManagersCount * 2;
		int completedSteps = coreManagersCount; // Setup is already done

		foreach (ManagerBase manager in managers)
		{
			if (DebugMode)
			{
				GD.Print($"Executing manager: {manager.GetManagerName()}");
			}
			await manager.ExecuteCall(loadingData);
			if (manager.includeInLoadingCalculation)
			{
				completedSteps++;
				loadingPercent = totalSteps > 0 ? (float)completedSteps / totalSteps : 1.0f;
			}
			// Yield to main thread to allow UI to update
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		
		loadingState = LoadingState.NONE;
		loadingPercent = 1.0f;
		EmitSignal(SignalName.CoreManagersLoaded);
	}

	#region Scene Management

	/// <summary>
	/// Attempts to change the current game scene, optionally saving state 
	/// and invoking a callback after the transition.
	/// </summary>
	public bool TryChangeScene(
		GameScene sceneName,
		Callable? callback,
		bool saveManagerData = true
	)
	{
		if (!scenePaths.ContainsKey(sceneName)) return false;

		loadingState = LoadingState.CHANGINGSCENES;
		loadingPercent = 0;
		UIManager.Instance?.ShowLoadingScreen();
		currentScene = sceneName;

		if (saveManagerData)
		{
			// handle autosaving state based on current session type.
			if (currentSavename.Contains("quickplay_internal"))
			{
				TryCreateSaveGame("quickplay_internal", sceneName);
				_loadFromAutosave = true;
			}
			else
			{
				TryCreateSaveGame(AutosaveName, sceneName);
				_loadFromAutosave = true;
			}

			_pendingSaveData = null;
		}
		else
		{
			// Store the current state in memory for the next scene instance to inherit.
			_loadFromAutosave = false;
			_pendingSaveData = PackageCurrentStateAsDictionary();
			_pendingSaveName = currentSavename;
		}

		CleanupManagers();

		if (GetTree().ChangeSceneToFile(scenePaths[sceneName]) != Error.Ok)
		{
			return false;
		}

		callback?.Call();
		return true;
	}

	/// <summary>
	/// Cycles through all child managers and the GameManager itself to 
	/// disconnect signals and clean up state.
	/// </summary>
	private void CleanupManagers()
	{
		GD.Print($"[Cleanup] Deinitializing managers for {currentScene}...");
		
		foreach (var m in managers)
		{
			if (GodotObject.IsInstanceValid(m))
			{
				m.DeinitializeCall();
			}
		}
		
		this.DeinitializeCall();
	}
	
	public override void Deinitialize()
	{
		GD.Print("[Cleanup] GameManager Deinitialized.");
	}


	private Godot.Collections.Dictionary<string, Variant> PackageCurrentStateAsDictionary()
	{
		List<ManagerBase> saveList = new();
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase mb) saveList.Add(mb);
		}
		saveList.Add(this);

		var managersDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var m in saveList)
		{
			managersDict[m.GetManagerName()] = m.Save();
		}

		return new Godot.Collections.Dictionary<string, Variant>
		{
			["version"] = 1,
			["scene"] = currentScene.ToString(),
			["managers"] = managersDict
		};
	}


	public void CheckGameState(Turn currentTurn)
	{
		var teamHolders = GridObjectManager.Instance.GetGridObjectTeamHolders();
		foreach (var kvp in teamHolders)
		{
			if (kvp.Value == null)
			{
				continue;
			}

			if (
				kvp.Key == Enums.UnitTeam.Enemy || kvp.Key == Enums.UnitTeam.Player
			)
			{
				// Check if any active units remain for the player or enemy team.
				if (kvp.Value.GridObjects[Enums.GridObjectState.Active].Count < 1)
				{
					EndGame();
				}
			}
		}
	}

	/// <summary>
	/// Handles scene transition when a game session ends
	/// </summary>
	private void EndGame()
	{
		if (currentSavename.Contains("quickplay_internal"))
		{
			TryChangeScene(GameScene.MainMenu, null);
		}
		else
		{
			TryChangeScene(GameScene.GlobeScene, null);
		}
	}

	#endregion
	

	#region Data Handling

	/// <summary>
	/// Initiates an asynchronous load of a specific game save file from disk.
	/// Prepares the pending data and triggers the scene change to the saved scene.
	/// </summary>
	public async Task<bool> TryLoadGameSaveAsync(string saveName)
	{
		string fullName = saveName.EndsWith(SaveExt) ? saveName : saveName + SaveExt;
		var path = saveDir.PathJoin(fullName);

		if (!FileAccess.FileExists(path)) return false;

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null) return false;

		try 
		{
			var root = file.GetVar().AsGodotDictionary<string, Variant>();
			
			// Store the loaded data to be injected into the new scene's managers.
			_pendingSaveData = root;
			_loadFromAutosave = false;
			_pendingSaveName = saveName.Replace(SaveExt, "");

			var sceneStr = root["scene"].AsString();
			if (Enum.TryParse<GameScene>(sceneStr, out var scene))
			{
				currentScene = scene;
				currentSavename = _pendingSaveName;

				CleanupManagers();

				GD.Print($"Loading Save: {saveName} | Targeted Scene: {scene}");
				UIManager.Instance?.ShowLoadingScreen();
				Error err = GetTree().ChangeSceneToFile(scenePaths[scene]);
				return err == Error.Ok;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to parse save: {e.Message}");
		}
    
		return false;
	}

	/// <summary>
	/// Injects loaded data into the current scene's sub-manager hierarchy.
	/// Ensures correct initialization order and data hand-off.
	/// </summary>
	private async Task PerformInternalLoadSequence(Godot.Collections.Dictionary<string, Variant> root)
	{
		GD.Print($"[Load] New GameManager instance detected static data. Injecting...");
		bool globalLoadingProcess = true; 

		if (!root.ContainsKey("managers")) return;
		var managersDict = root["managers"].AsGodotDictionary<string, Variant>();
		
		// Load this GameManager's data first to establish base state.
		string myKey = GetManagerName();
		if (managersDict.ContainsKey(myKey))
		{
			this.Load(managersDict[myKey].AsGodotDictionary<string, Variant>());
		}

		// Identify managers in the new scene 
		List<ManagerBase> managersNow = new();
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase mb) managersNow.Add(mb);
		}


		managersNow.Sort((a, b) => {
			if (a is GlobeHexGridManager) return -1;
			if (b is GlobeHexGridManager) return 1;
			return 0;
		});

		foreach (var m in managersNow)
		{
			string key = m.GetManagerName();
			if (managersDict.ContainsKey(key))
			{
				m.Load(managersDict[key].AsGodotDictionary<string, Variant>());
			}
			else
			{
				// clear if no data was found for this manager.
				m.Load(null); 
			}
		}
    
		// 5. Run standard lifecycle (Setup -> Execute) for all managers in the new scene.
		loadingState = LoadingState.SETTINGUPMANAGERS;
		int coreManagersCount = 0;
		foreach (var m in managersNow) if (m.includeInLoadingCalculation) coreManagersCount++;
		int totalSteps = coreManagersCount * 2;
		int completedSteps = 0;

		foreach (var m in managersNow)
		{
			await m.SetupCall(globalLoadingProcess);
			if (m.includeInLoadingCalculation)
			{
				completedSteps++;
				loadingPercent = totalSteps > 0 ? (float)completedSteps / totalSteps : 1.0f;
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		loadingState = LoadingState.EXECUTINGMANAGERS;
		foreach (var m in managersNow)
		{
			await m.ExecuteCall(globalLoadingProcess);
			if (m.includeInLoadingCalculation)
			{
				completedSteps++;
				loadingPercent = totalSteps > 0 ? (float)completedSteps / totalSteps : 1.0f;
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		
		loadingState = LoadingState.NONE;
		loadingPercent = 1.0f;
		EmitSignal(SignalName.CoreManagersLoaded);
		GD.Print("[Load] Data hand-off complete.");
	}

	public bool TryCreateSaveGame(
		string saveName,
		GameScene scene,
		bool isNewGame = false
	)
	{
		EnsureSaveDir();
		if (!saveName.EndsWith(SaveExt))
		{
			saveName += SaveExt;
		}

		if (
			!this.TryGetAllComponentsInChildren<ManagerBase>(
				out List<ManagerBase> saveList
			)
		)
		{
			return false;
		}

		saveList.Add(this);

		Godot.Collections.Dictionary<string, Variant> managersDict = new();
		
		// 1. Try to load existing data to merge
		string fullPath = saveDir + saveName;
		if (!isNewGame && FileAccess.FileExists(fullPath))
		{
			using var fileRead = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
			if (fileRead != null)
			{
				try 
				{
					var oldRoot = fileRead.GetVar().AsGodotDictionary<string, Variant>();
					if (oldRoot.ContainsKey("managers"))
					{
						managersDict = oldRoot["managers"].AsGodotDictionary<string, Variant>();
					}
				}
				catch { /* Ignore corrupt existing saves, start fresh */ }
			}
		}

		// 2. Overwrite/Update with current managers
		foreach (var m in saveList)
		{
			managersDict[m.GetManagerName()] = m.Save();
		}

		var root = new Godot.Collections.Dictionary<string, Variant>
		{
			["version"] = 1,
			["isNewGame"] = isNewGame,
			["scene"] = scene.ToString(),
			["managers"] = managersDict,
		};

		using var file = FileAccess.Open(
			fullPath,
			FileAccess.ModeFlags.Write
		);
		if (file == null)
		{
			return false;
		}

		file.StoreVar(root);
		EmitSignal(SignalName.GameSavesChanged);
		return true;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			["mapSize"] = mapSize,
			["unitCounts"] = unitCounts,
			["currentScene"] = (int)currentScene
		};
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data.ContainsKey("mapSize"))
		{
			mapSize = (Vector2I)data["mapSize"];
		}

		if (data.ContainsKey("unitCounts"))
		{
			unitCounts = (Vector2I)data["unitCounts"];
		}
		
	}

	#endregion

	#region Utilities

	private void EnsureSaveDir()
	{
		if (!DirAccess.DirExistsAbsolute(saveDir))
		{
			DirAccess.MakeDirRecursiveAbsolute(saveDir);
		}
	}

	public List<string> GetSaveFileDisplayNames()
	{
		List<string> names = new();
		using var dir = DirAccess.Open(saveDir);
		if (dir == null)
		{
			return names;
		}

		dir.ListDirBegin();
		string f = dir.GetNext();
		while (f != "")
		{
			if (!dir.CurrentIsDir() && f.EndsWith(SaveExt))
			{
				names.Add(f.Replace(SaveExt, ""));
			}

			f = dir.GetNext();
		}

		return names;
	}

	public bool TryDeleteSaveGame(string saveName)
	{
		string f = saveName.EndsWith(SaveExt) ? saveName : saveName + SaveExt;
		using var dir = DirAccess.Open(saveDir);
		if (dir == null || dir.Remove(f) != Error.Ok)
		{
			return false;
		}

		EmitSignal(SignalName.GameSavesChanged);
		return true;
	}

	#endregion


	#region Get/Set Functions

	public string GetCurrentSaveName() => currentSavename;
	
	public void SetCurrentSaveName(string saveName) => currentSavename = saveName;

	#endregion
	
		

	#endregion
}