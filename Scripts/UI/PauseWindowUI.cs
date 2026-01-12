using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class PauseWindowUI : UIWindow
{
	[Export] protected Button _resumeButton;
	[Export] protected Button _saveButton;
	[Export] protected Button _loadButton;
	[Export] protected Button _settingsButton;
	[Export] protected Button _quitMenuButton;
	[Export] protected Button _quitGameButton;
	[Export] protected UIWindow _gameSaveUI;
	protected override Task _Setup()
	{
		
		base._Setup();
		
		if(!_resumeButton.IsConnected(Button.SignalName.Pressed, Callable.From(ResumeButtonPressed)))
			_resumeButton.Pressed += ResumeButtonPressed;
		
		if(!_saveButton.IsConnected(Button.SignalName.Pressed, Callable.From(SaveButtonPressed)))
			_saveButton.Pressed += SaveButtonPressed;
		
		if(!_loadButton.IsConnected(Button.SignalName.Pressed, Callable.From(LoadButtonPressed)))
			_loadButton.Pressed += LoadButtonPressed;
		
		if(!_settingsButton.IsConnected(Button.SignalName.Pressed, Callable.From(SettingsButtonPressed)))
			_settingsButton.Pressed += SettingsButtonPressed;
		
		if(!_quitMenuButton.IsConnected(Button.SignalName.Pressed, Callable.From(QuitmenuButtonPressed)))
			_quitMenuButton.Pressed += QuitmenuButtonPressed;
		
		if(!_quitGameButton.IsConnected(Button.SignalName.Pressed, Callable.From(QuitGameButtonPressed)))
			_quitGameButton.Pressed += QuitGameButtonPressed;
		return Task.CompletedTask;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if(_resumeButton.IsConnected(Button.SignalName.Pressed, Callable.From(ResumeButtonPressed)))
			_resumeButton.Pressed -= ResumeButtonPressed;
		
		if(_saveButton.IsConnected(Button.SignalName.Pressed, Callable.From(SaveButtonPressed)))
			_saveButton.Pressed -= SaveButtonPressed;
		
		if(_loadButton.IsConnected(Button.SignalName.Pressed, Callable.From(LoadButtonPressed)))
			_loadButton.Pressed -= LoadButtonPressed;
		
		if(_settingsButton.IsConnected(Button.SignalName.Pressed, Callable.From(SettingsButtonPressed)))
			_settingsButton.Pressed -= SettingsButtonPressed;
		
		if(_quitMenuButton.IsConnected(Button.SignalName.Pressed, Callable.From(QuitmenuButtonPressed)))
			_quitMenuButton.Pressed -= QuitmenuButtonPressed;
		
		if(_quitGameButton.IsConnected(Button.SignalName.Pressed, Callable.From(QuitGameButtonPressed)))
			_quitGameButton.Pressed -= QuitGameButtonPressed;
		base._ExitTree();
	}

	private void ResumeButtonPressed()
	{
		if (_gameSaveUI.IsShown)
		{
			_gameSaveUI.HideCall();
		}
		this.HideCall();
	}
	
	private void SaveButtonPressed()
	{
		_gameSaveUI.Toggle();
	}
	
	private void LoadButtonPressed()
	{
		//TODO: Make diffrent functionality
		_gameSaveUI.Toggle();
	}
	
	private void SettingsButtonPressed()
	{
		//TODO: Implement Settings
	}
	
	private void QuitmenuButtonPressed()
	{
		GameManager.Instance.TryChangeScene(GameManager.GameScene.MainMenu, null, false);
	}
	
	private void QuitGameButtonPressed()
	{
		GetTree().Quit();
	}


	protected override void _Show()
	{
		base._Show();
		if (_gameSaveUI != null)
		{
			_gameSaveUI.ShowCall();
		}
	}

	protected override void _Hide()
	{
		base._Hide();
		if (_gameSaveUI != null)
		{
			_gameSaveUI.HideCall();
		}
	}
}
