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
	public Vector2I unitCounts = new Vector2I(1, 1);

	public enum GameScene
	{
		NONE,
		MainMenu,
		BattleScene,
		GlobeScene
	}

	public GameScene currentScene = GameScene.MainMenu;

	private string currentSavename = "new SaveGame";
	private const string SaveExt = ".sav";

	private static Godot.Collections.Dictionary<string, Variant> _pendingSaveData =
		null;

	private static string _pendingSaveName = "";

	[Signal]
	public delegate void GameSavesChangedEventHandler();

	public override string GetManagerName() => "GameManager";

	public override async void _Ready()
	{
		base._Ready();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (_pendingSaveData != null)
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
			await manager.SetupCall(loadingData);
		}

		await ExecuteCall(loadingData);
	}

	protected override async Task _Execute(bool loadingData)
	{
		foreach (ManagerBase manager in managers)
		{
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

		// 1. Update destination state
		currentScene = sceneName;

		// 2. Persistent State hand-off (Memory Only)
		if (saveManagerData)
		{
			_pendingSaveData = PackageCurrentStateAsDictionary();
			_pendingSaveName = currentSavename;
		}

		// 3. CLEANUP: Deinitialize all managers before leaving the scene
		CleanupManagers();

		// 4. Change Scene
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

	private void EndGame() => TryChangeScene(GameScene.MainMenu, null);

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
		
		// 1. Load this GameManager's data first
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
		GameScene scene = GameScene.NONE,
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

		var managersDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var m in saveList)
		{
			managersDict[m.GetManagerName()] = m.Save();
		}

		var root = new Godot.Collections.Dictionary<string, Variant>
		{
			["version"] = 1,
			["isNewGame"] = isNewGame, // Store the flag
			["scene"] =
				scene == GameScene.NONE ? currentScene.ToString() : scene.ToString(),
			["managers"] = managersDict,
		};

		using var file = FileAccess.Open(
			saveDir + saveName,
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

		if (data.ContainsKey("currentScene"))
		{
			currentScene = (GameScene)data["currentScene"].AsInt32();
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
}