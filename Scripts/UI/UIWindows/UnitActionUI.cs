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
	private List<GridStatBarUI> _statBars = new List<GridStatBarUI>();


	protected override async Task _Setup()
	{
		await base._Setup();
		GridObject selectedGridObject = GridObjectManager.Instance.CurrentPlayerGridObject;
		
		foreach (UIElement uiElement in uiElements)
		{
			if (uiElement is ActionButtonUI actionButtonUI)
			{
				_actionButtons.Add(actionButtonUI);
			}
			
			if (uiElement is GridStatBarUI statBarUI)
				{
				_statBars.Add(statBarUI);
				statBarUI.SetupStatBar(selectedGridObject);
				}
		}
		GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).SelectedGridObjectChanged += OnSelectedGridObjectChanged;
	}

	private void OnSelectedGridObjectChanged(GridObject gridObject)
	{
		UpdateActionButtons(gridObject);
		UpdateStatBars(gridObject);
	}

	private void UpdateActionButtons(GridObject gridObject)
	{
		ClearActionButtons();
		if (gridObject == null) return;
		if (_actionButtonContainer == null) return;
		
		if(!gridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode)) return;
		
		ActionDefinition[] gridObjectActions = gridObjectActionsNode.ActionDefinitions;
		if(gridObjectActions ==  null || gridObjectActions.Length < 1) return;
		
		
		
		foreach (ActionDefinition action in gridObjectActions)
		{
			if(action.GetIsUIAction())
				CreateActionButton(action);
		}
	}

	private void UpdateStatBars(GridObject gridObject)
	{
		if (gridObject == null) return;

		foreach (GridStatBarUI statBarUI in _statBars)
		{
			statBarUI.SetupStatBar(gridObject);
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
