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
	[Export] Dictionary<gameScene, string> scenePaths = new Dictionary<gameScene, string>();
	private Node sceneHolder;
	public override async void _Ready()
	{
		base._Ready();
		await SetupCall();
	}



	protected override async Task _Setup()
	{
		// Ensure the exported array exists even if not set in the Inspector
		managers ??= new Array<ManagerBase>();

		// Collect child managers (avoid duplicates if you ever call _Setup again)
		foreach (Node child in GetChildren())
		{
			if (child is ManagerBase manager && !managers.Contains(manager))
				managers.Add(manager);
		}

		// Await each manager's setup
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
			GD.Print($"Executing {manager.Name}");
			await manager.ExecuteCall();
		}
	}

	public bool TryChangeScene(gameScene sceneName, Callable callback, bool saveOldScene = false)
	{
		if (!scenePaths.ContainsKey(sceneName)) return false;

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
			callback.Call();
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
	}
	
	protected override void GetInstanceData(ManagerData data)
	{
		mapSize = (Vector2I)data.managerData["mapSize"];
		unitCounts = (Vector2I)data.managerData["unitCounts"];
	}

	public override ManagerData SetInstanceData()
	{
		ManagerData data = new ManagerData();
		data.managerData.Add("mapSize", mapSize);
		data.managerData.Add("unitCounts", unitCounts);
		return data;
	}
}