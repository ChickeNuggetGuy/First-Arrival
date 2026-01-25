using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class GameSaveUI : UIWindow
{
	[Export] private GameManager.GameScene targetScene;
	[Export] private ItemList _itemList;
	[Export] private TextEdit _saveNameEdit;
	
	[Export] private Button _saveButton;
	[Export] private Button _loadButton;
	[Export] private Button _deleteButton;


	protected override Task _Setup()
	{
		if(!_saveButton.IsConnected(Button.SignalName.Pressed, Callable.From(SaveButtonOnPressed)))
			_saveButton.Pressed += SaveButtonOnPressed;
		
		if(!_loadButton.IsConnected(Button.SignalName.Pressed, Callable.From(LoadButtonOnPressed)))
			_loadButton.Pressed += LoadButtonOnPressed;
		
		if(!_deleteButton.IsConnected(Button.SignalName.Pressed, Callable.From(DeleteButtonOnPressed)))
			_deleteButton.Pressed += DeleteButtonOnPressed;
		
		GameManager.Instance.GameSavesChanged += LoadSaveGameData;
		LoadSaveGameData();
		return base._Setup();
	}

	private void LoadSaveGameData()
	{
		_itemList.Clear();
		List<string> saveFileNames = GameManager.Instance.GetSaveFileDisplayNames();

		if (saveFileNames == null || saveFileNames.Count <= 0)
			return;

		foreach (string saveFileName in saveFileNames)
		{
			_itemList.AddItem(saveFileName);
		}
	}

	private void DeleteButtonOnPressed()
	{
		if (_itemList.GetSelectedItems().Length < 1) return;
		string saveName =_itemList.GetItemText(_itemList.GetSelectedItems()[0]);

		GD.Print("Test");
		GameManager.Instance.TryDeleteSaveGame(saveName);
		
	}

	private async void LoadButtonOnPressed()
	{
		if (_itemList.GetSelectedItems().Length < 1)
		{
			GD.Print($"selected item null");
			return;
		}

		string saveName = _itemList.GetItemText(_itemList.GetSelectedItems()[0]);
		GD.Print($"Load {saveName}:");
		bool ok = await GameManager.Instance.TryLoadGameSaveAsync(saveName);
		
	}

	private void SaveButtonOnPressed()
	{
		string saveName = "";
		if (_itemList.GetSelectedItems().Length > 0)
		{
			
			saveName = _itemList.GetItemText(_itemList.GetSelectedItems()[0]);
			GD.Print(GameManager.Instance.TryCreateSaveGame(saveName, targetScene));
			return;
		}
		else if (_saveNameEdit.Text.Length > 0)
		{
			saveName = _saveNameEdit.Text;
		}
		
		if (saveName.Length > 0)
		{
			GD.Print(GameManager.Instance.TryCreateSaveGame(saveName, targetScene));
		}
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		_saveButton.Pressed -= SaveButtonOnPressed;
		_loadButton.Pressed -= LoadButtonOnPressed;
		_deleteButton.Pressed -= DeleteButtonOnPressed;

		if (GameManager.Instance != null)
			GameManager.Instance.GameSavesChanged -= LoadSaveGameData;
	}
}
