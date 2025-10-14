using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class MainMenuUI : UIWindow
{
	#region Variables
	[Export] public Button newGameButton;
	[Export] public Button quickPlayButton;
	[Export] public Button quitButton;
	[Export] public UIWindow gameSettingsMenu;
	#endregion
	
	#region Functions
	public MainMenuUI()
	{
	}

	public override void _EnterTree()
	{
		base._EnterTree();
		newGameButton.Pressed += NewGameButtonOnPressed;
		quickPlayButton.Pressed += QuickPlayButtonOnPressed;
		quitButton.Pressed += QuitButtonOnPressed;
	}



	public override void _ExitTree()
	{
		base._ExitTree();
		newGameButton.Pressed -= NewGameButtonOnPressed;
		quickPlayButton.Pressed -= QuickPlayButtonOnPressed;
		quitButton.Pressed -= QuitButtonOnPressed;
	}

	protected override Task _Setup()
	{
		base._Setup();
		return Task.CompletedTask;
	}

	
	private void QuickPlayButtonOnPressed()
	{
		GameManager.Instance.mapSize = new Vector2I(GD.RandRange(2,3), GD.RandRange(2,3));
		GameManager.Instance.unitCounts = new Vector2I(GD.RandRange(2,4), GD.RandRange(2,6));
		
		GameManager.Instance.TryChangeScene(GameManager.gameScene.BattleScene, false);
	}

	private void Test()
	{
		
	}
	private void QuitButtonOnPressed()
	{
		GetTree().Quit();
	}



	private void NewGameButtonOnPressed()
	{
		gameSettingsMenu.ShowCall();
	}

	#endregion
	
	
}
