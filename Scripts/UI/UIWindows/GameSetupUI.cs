using Godot;
using System;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class GameSetupUI : UIWindow
{
	[Export] public SpinBox mapSizeX;
	[Export] public SpinBox mapSizeY;
	[Export] public SpinBox PlayerUnitCounts;
	[Export] public SpinBox EnemyUnitCounts;
	[Export] public Button startGameButton;
	
	
	private Vector2I mapSize = Vector2I.Zero;
	private Vector2I unitCounts = Vector2I.Zero;
	
	public override void _EnterTree()
	{
		base._EnterTree();
		startGameButton.Pressed += StartGameButtonOnPressed;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		startGameButton.Pressed -= StartGameButtonOnPressed;
	}
	
	private void StartGameButtonOnPressed()
	{
		mapSize = new Vector2I(Mathf.RoundToInt(mapSizeX.Value),  Mathf.RoundToInt(mapSizeY.Value));
		unitCounts = new Vector2I(Mathf.RoundToInt(PlayerUnitCounts.Value), Mathf.RoundToInt(EnemyUnitCounts.Value));

		GameManager.Instance.TryChangeScene(GameManager.gameScene.BattleScene,new Callable(this ,nameof(onBattleSceneLoaded)), false);
	}

	public void onBattleSceneLoaded()
	{
		GameManager.Instance.mapSize = mapSize;
		GameManager.Instance.unitCounts = unitCounts;
	}

}
