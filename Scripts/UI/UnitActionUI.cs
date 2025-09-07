using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class UnitActionUI : UIWindow
{
	[Export] private GridContainer _actionButtonContainer;
	[Export] PackedScene _actionButtonScene;
	List<ActionButtonUI>  _actionButtons = new List<ActionButtonUI>();
	
	[Export] private VBoxContainer _statBarContainer;


	protected override async Task _Setup()
	{
		await base._Setup();
		foreach (UIElement uiElement in uiElements)
		{
			if (uiElement is ActionButtonUI actionButtonUI)
			{
				_actionButtons.Add(actionButtonUI);
			}
		}
		GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).SelectedGridObjectChanged += OnSelectedGridObjectChanged;
	}

	private void OnSelectedGridObjectChanged(GridObject gridObject)
	{
		UpdateActionButtons(gridObject);
	}

	private void UpdateActionButtons(GridObject gridObject)
	{
		ClearActionButtons();
		if (gridObject == null) return;
		if (_actionButtonContainer == null) return;
		
		ActionDefinition[] gridObjectActions = gridObject.ActionDefinitions;
		if(gridObjectActions ==  null || gridObjectActions.Length < 1) return;
		
		
		
		foreach (ActionDefinition action in gridObjectActions)
		{
			if(action.GetIsUIAction())
				CreateActionButton(action);
		}
	}


	private async Task CreateActionButton(ActionDefinition actionDefinition)
	{
		ActionButtonUI newActionButton = _actionButtonScene.Instantiate() as ActionButtonUI;
		if (newActionButton == null)
		{
			GD.Print("action button null");
			return;
		}
		newActionButton.actionDefinition = actionDefinition;


		_actionButtons.Add(newActionButton);
		_actionButtonContainer.AddChild(newActionButton);
		await newActionButton.SetupCall();
	}
	
	private void ClearActionButtons()
	{
		foreach (ActionButtonUI actionButtonUI in _actionButtons)
		{
			actionButtonUI.QueueFree();
		}
		
		_actionButtons.Clear();
	}
}
