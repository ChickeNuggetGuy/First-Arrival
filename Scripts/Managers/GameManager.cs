using System;
using System.Collections.Generic;
using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class GameManager : Manager<GameManager>
{
	[Export] public Array<ManagerBase> managers = new();
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

	[Export] public GameScene currentScene;

	private string currentSavename = "new SaveGame";
	private const string SaveExt = ".sav";
	private const string AutosaveName = "autosave";

	private static Godot.Collections.Dictionary<string, Variant> _pendingSaveData =
		null;
	private static bool _loadFromAutosave = false;

	private static string _pendingSaveName = "";

	[Signal]
	public delegate void GameSavesChangedEventHandler();

	public override string GetManagerName() => "GameManager";

	public override async void _Ready()
	{
		base._Ready();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (_loadFromAutosave)
		{
			_loadFromAutosave = false;
			await LoadAutosaveInternal();
		}
		else if (_pendingSaveData != null)
		{
			currentSavename = _pendingSaveName;
			var data = _pendingSaveData;
			_pendingSaveData = null;
			_pendingSaveName = "";
			await PerformInternalLoadSequence(data);
		}
		else
		{
			await SetupCall(false);
		}
	}

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
		// Fallback if autosave fails
		await SetupCall(false);
	}

	protected override async Task _Setup(bool loadingData)
	{
		managers ??= new Array<ManagerBase>();
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase m && !managers.Contains(m))
			{
				managers.Add(m);
			}
		}

		foreach (ManagerBase manager in managers)
		{
			if (DebugMode)
			{
				GD.Print($"Setting up manager: {manager.GetManagerName()}");
			}
			await manager.SetupCall(loadingData);
		}

		await ExecuteCall(loadingData);
	}

	protected override async Task _Execute(bool loadingData)
	{
		foreach (ManagerBase manager in managers)
		{
			if (DebugMode)
			{
				GD.Print($"Executing manager: {manager.GetManagerName()}");
			}
			await manager.ExecuteCall(loadingData);
		}
	}

	#region Scene Management

	public bool TryChangeScene(
		GameScene sceneName,
		Callable? callback,
		bool saveManagerData = true
	)
	{
		if (!scenePaths.ContainsKey(sceneName)) return false;

		currentScene = sceneName;

		if (saveManagerData)
		{
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
		// Find all managers that are children of THIS manager instance
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
				if (kvp.Value.GridObjects[Enums.GridObjectState.Active].Count < 1)
				{
					EndGame();
				}
			}
		}
	}

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
			
			_pendingSaveData = root;
			_loadFromAutosave = false;
			_pendingSaveName = saveName.Replace(SaveExt, "");

			var sceneStr = root["scene"].AsString();
			if (Enum.TryParse<GameScene>(sceneStr, out var scene))
			{
				currentScene = scene;
				currentSavename = _pendingSaveName;

				// 1. CLEANUP: We are about to swap scenes, clean up current nodes
				CleanupManagers();

				GD.Print($"Loading Save: {saveName} | Targeted Scene: {scene}");
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

	private async Task PerformInternalLoadSequence(Godot.Collections.Dictionary<string, Variant> root)
	{
		GD.Print($"[Load] New GameManager instance detected static data. Injecting...");
		bool globalLoadingProcess = true; 

		if (!root.ContainsKey("managers")) return;
		var managersDict = root["managers"].AsGodotDictionary<string, Variant>();
		
		// Load this GameManager's data first
		string myKey = GetManagerName();
		if (managersDict.ContainsKey(myKey))
		{
			this.Load(managersDict[myKey].AsGodotDictionary<string, Variant>());
		}

		// 2. Identify managers in the NEW scene (children of this new GameManager)
		List<ManagerBase> managersNow = new();
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase mb) managersNow.Add(mb);
		}

		// 3. Critical: Load Order Sorting
		// Re-adding the sort to ensure Grid/Data managers initialize before logic managers
		managersNow.Sort((a, b) => {
			if (a is GlobeHexGridManager) return -1;
			if (b is GlobeHexGridManager) return 1;
			return 0;
		});

		// 4. Inject the passed-down data into the new nodes
		foreach (var m in managersNow)
		{
			string key = m.GetManagerName();
			if (managersDict.ContainsKey(key))
			{
				m.Load(managersDict[key].AsGodotDictionary<string, Variant>());
			}
			else
			{
				m.Load(null); // Explicitly clear if no data passed
			}
		}
    
		// 5. Run standard lifecycle (Setup -> Execute)
		foreach (var m in managersNow) await m.SetupCall(globalLoadingProcess);
		foreach (var m in managersNow) await m.ExecuteCall(globalLoadingProcess);
		
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
			["isNewGame"] = isNewGame, // Store the flag
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

		// if (data.ContainsKey("currentScene"))
		// {
		// 	currentScene = (GameScene)data["currentScene"].AsInt32();
		// }
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
}