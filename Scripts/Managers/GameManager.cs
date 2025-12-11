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
	[Export] private Array<ManagerBase> managers;

	public Vector2I mapSize = new Vector2I(1, 1);
	public Vector2I unitCounts = new Vector2I(1, 1);
	
	public enum gameScene {MainMenu, BattleScene, GlobeScene}
	[Export] Godot.Collections.Dictionary<gameScene, string> scenePaths = new Godot.Collections.Dictionary<gameScene, string>();
	[Export] private string currentSavename = "new SaveGame";
	private Node sceneHolder;
	[Export, ExportGroup("Data Handling")] protected string savePath = "/Users/malikhawkins/Godot Projects /first-arrival/TempSaves/";
		
	#region Signals

	[Signal] public delegate void GameSavesChangedEventHandler();
	#endregion
	public override string GetManagerName() =>  "GameManager";

	public override async void _Ready()
	{
		base._Ready();
		await SetupCall();
	}
	
	protected override async Task _Setup()
	{
		managers ??= new Array<ManagerBase>();
		
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase manager && !managers.Contains(manager))
				managers.Add(manager);
		}


		foreach (ManagerBase manager in managers)
			await manager.SetupCall();

		// Optionally proceed to Execute after setup
		await ExecuteCall();
	}

	protected override async Task _Execute()
	{
		GD.Print("Execute");
		foreach (ManagerBase manager in managers)
		{
			await manager.ExecuteCall();
			GD.Print($"Executed {manager.Name}");
		}
	}
	
	public bool TryChangeScene(gameScene sceneName, Callable? callback, bool saveOldScene = false, bool saveManagerData = true)
	{
		if (!scenePaths.ContainsKey(sceneName)) return false;

		if (saveManagerData)
		{
			GD.Print(TryCreateSaveGame(currentSavename));
		}
		if (saveOldScene)
		{
			sceneHolder = GetTree().CurrentScene;
		}
		if (GetTree().ChangeSceneToFile(scenePaths[sceneName]) != Error.Ok)
		{
			return false;
		}
		else
		{
			if(callback != null)
				callback.Value.Call();
			return true;
		}
	}

	public void CheckGameState(Turn currentTurn)
	{
		Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder> teamHolders =
			GridObjectManager.Instance.GetGridObjectTeamHolders();
		
		foreach (var kvp in teamHolders)
		{
			if(kvp.Value == null) continue;
			if (kvp.Key == Enums.UnitTeam.Enemy || kvp.Key == Enums.UnitTeam.Player)
			{
				if (kvp.Value.GridObjects[Enums.GridObjectState.Active].Count < 1)
				{
					//All GridObjects on team Inactive, Game Should end!
					EndGame();
				}
			}
		}
	}

	private void EndGame()
	{
		GD.Print("EndGame");
		GD.Print(TryChangeScene(gameScene.MainMenu, null));
	}

	#region Data Saving
	public bool TryCreateSaveGame(string saveName)
	{
		// Ensure the file has a .json extension
		if (!saveName.EndsWith(".json"))
		{
			saveName += ".json";
		}
    
		string fullPath = savePath + saveName;
		GD.Print($"Attempting to save to: {fullPath}");

		using var saveFile = FileAccess.Open(fullPath, FileAccess.ModeFlags.Write);
    
		if (saveFile == null)
		{
			Error error = FileAccess.GetOpenError();
			GD.PrintErr($"Failed to open save file. Error: {error}");
			return false;
		}

		if(!this.TryGetAllComponentsInChildren<ManagerBase>(out List<ManagerBase> saveList))
		{
			GD.PrintErr("Failed to get components for saving");
			return false;
		}
    
		foreach (var saveNode in saveList)
		{
			var nodeData = saveNode.Save();
			var jsonString = Json.Stringify(nodeData);
			saveFile.StoreLine(jsonString);
		}

		EmitSignal(SignalName.GameSavesChanged);
		GD.Print($"Successfully saved game to: {fullPath}");
		return true;
	}
	
	public bool TryGetSaveGameData(string saveName, out Godot.Collections.Dictionary<string, Variant>  data)
	{
		string path = this.savePath + saveName;
		data = null;
		if (!FileAccess.FileExists(path))
		{
			return false;
		}
		
		using var saveFile = FileAccess.Open( path, FileAccess.ModeFlags.Read);
		
		if (saveFile == null)
		{
			data = null;
			return false;
		}
		
		while (saveFile.GetPosition() < saveFile.GetLength())
		{
			var jsonString = saveFile.GetLine();

			// Creates the helper class to interact with JSON.
			var json = new Json();
			var parseResult = json.Parse(jsonString);
			if (parseResult != Error.Ok)
			{
				GD.Print($"JSON Parse Error: {json.GetErrorMessage()} in {jsonString} at line {json.GetErrorLine()}");
				continue;
			}

			// Get the data from the JSON object.
			var nodeData = new Godot.Collections.Dictionary<string, Variant>((Godot.Collections.Dictionary)json.Data);
			data = nodeData;
			// Firstly, we need to create the object and add it to the tree and set its position.
			var newObjectScene = GD.Load<PackedScene>(nodeData["Filename"].ToString());
			var newObject = newObjectScene.Instantiate<Node>();
			GetNode(nodeData["Parent"].ToString()).AddChild(newObject);
			newObject.Set(Node2D.PropertyName.Position, new Vector2((float)nodeData["PosX"], (float)nodeData["PosY"]));

			// Now we set the remaining variables.
			foreach (var (key, value) in nodeData)
			{
				if (key == "Filename" || key == "Parent" || key == "PosX" || key == "PosY")
				{
					continue;
				}
				newObject.Set(key, value);
			}
		}
		return true;
	}


	// For internal use (loading, deleting files, etc.)
	public List<string> GetSaveFileNames()
	{
		var fileNames = new List<string>();
    
		using var dir = DirAccess.Open(savePath);
		if (dir == null)
		{
			GD.PrintErr($"Failed to open save path: {savePath}");
			return fileNames;
		}

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (!string.IsNullOrEmpty(fileName))
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
			{
				fileNames.Add(fileName);
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();
    
		return fileNames;
	}

// For UI display
	public List<string> GetSaveFileDisplayNames()
	{
		var displayNames = new List<string>();
		var fileNames = GetSaveFileNames();
    
		foreach (string fileName in fileNames)
		{
			// Remove .json extension for display
			string displayName = fileName.Substring(0, fileName.Length - 5); // Remove ".json"
			displayNames.Add(displayName);
		}
    
		return displayNames;
	}
	
	
	public void DeleteSaveGame()
	{
		
	}
	#endregion

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		Godot.Collections.Dictionary<string,Variant> data = new Godot.Collections.Dictionary<string,Variant>();
		data.Add("mapSize", mapSize);
		data.Add("unitCounts", unitCounts);
		data.Add("scenePaths", scenePaths as Godot.Collections.Dictionary<gameScene, string>);
		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string,Variant>  data)
	{
		mapSize = (Vector2I)data["mapSize"];
		unitCounts = (Vector2I)data["unitCounts"];
		scenePaths = (Godot.Collections.Dictionary<gameScene, string>)data["scenePaths"];
	}
}