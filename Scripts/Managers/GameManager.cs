using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;
using Array = Godot.Collections.Array;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class GameManager : Manager<GameManager>
{
	[Export] private Array<ManagerBase> managers;
	
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

	public bool TryChangeScene(gameScene sceneName, bool saveOldScene = false)
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
			return true;
		}
	}
}