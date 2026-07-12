using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class MainMenuUI : UIWindow
{
	#region Variables
	[Export] public Button newGameButton;
	[Export] public Button loadGameButton;
	[Export] public Button quickPlayButton;
	[Export] public Button quitButton;
	[Export] public UIWindow gameSettingsMenu;
	[Export] public GameSaveUI gameSavesWindow;
	#endregion
	
	#region Functions
	public MainMenuUI()
	{
	}

	public override void _EnterTree()
	{
		base._EnterTree();
		newGameButton.Pressed += NewGameButtonOnPressed;
		loadGameButton.Pressed += LoadGameButtonOnPressed;
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

	private void LoadGameButtonOnPressed()
	{
		gameSavesWindow.Toggle();
	}
	
	private void QuickPlayButtonOnPressed()
	{
		SavesManager.Instance.currentSavename = "quickplay_internal";
		GameManager.Instance.mapSize = new Vector2I(GD.RandRange(2,3), GD.RandRange(2,3));
		GameManager.Instance.unitCounts = new Vector2I(GD.RandRange(2,4), GD.RandRange(2,5));
		
		GameManager.Instance.TryChangeScene(GameManager.GameScene.BattleScene, false);
	}
	
	
	private void QuitButtonOnPressed()
	{
		GetTree().Quit();
	}



	private void NewGameButtonOnPressed()
	{
		GameManager.Instance.StartNewGame(GameManager.GameScene.GlobeScene);
	}


	#endregion
	
	
}
