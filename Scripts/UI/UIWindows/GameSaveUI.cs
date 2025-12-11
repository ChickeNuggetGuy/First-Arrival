using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class GameSaveUI : UIWindow
{
	[Export] private ItemList _itemList;
	[Export] private TextEdit _saveNameEdit;
	
	[Export] private Button _saveButton;
	[Export] private Button _loadButton;
	[Export] private Button _deleteButton;


	protected override Task _Setup()
	{
		_saveButton.Pressed += SaveButtonOnPressed;
		_loadButton.Pressed += LoadButtonOnPressed;
		_deleteButton.Pressed += DeleteButtonOnPressed;
		
		GameManager.Instance.GameSavesChanged += LoadSaveGameData;
		LoadSaveGameData();
		return base._Setup();
	}

	private void LoadSaveGameData()
	{
		_itemList.Clear();
		List<string> saveFileNames = GameManager.Instance.GetSaveFileDisplayNames();
		
		if (saveFileNames == null ||  saveFileNames.Count < 0) return;

		foreach (string saveFileName in saveFileNames)
		{
			_itemList.AddItem(saveFileName);
		}
	}

	private void DeleteButtonOnPressed()
	{
		if (_itemList.GetSelectedItems().Length < 1) return;
		
	}

	private void LoadButtonOnPressed()
	{
		if (_itemList.GetSelectedItems().Length < 1) return;
		
		string saveName =_itemList.GetItemText(_itemList.GetSelectedItems()[0]);
		GD.Print(saveName);
	}

	private void SaveButtonOnPressed()
	{
		if (_saveNameEdit.Text.Length < 1) return;
		
		string saveName = _saveNameEdit.Text;
	 GD.Print(GameManager.Instance.TryCreateSaveGame(saveName));
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		_saveButton.Pressed -= SaveButtonOnPressed;
		_loadButton.Pressed -= LoadButtonOnPressed;
		_deleteButton.Pressed -= DeleteButtonOnPressed;
	}
}
