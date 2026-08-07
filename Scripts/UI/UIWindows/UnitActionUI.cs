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
	[Export] private Label unitName;
	[Export] private TextureRect _unitIcon;
	List<ActionButtonUI>  _actionButtons = new List<ActionButtonUI>();
	
	[Export] private VBoxContainer _statBarContainer;
	private List<GridStatBarUI> _statBars = new List<GridStatBarUI>();


	protected override async Task _Setup()
	{
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
		ActionManager.Instance.ActionPreviewChanged += OnActionPreviewChanged;
	}
	
	protected override async Task DrawUI()
	{
	}


	private void OnSelectedGridObjectChanged(GridObject gridObject)
	{
		if (gridObject == null)
		{
			unitName.Text = "";
			_unitIcon.Texture = null;
			ClearActionButtons();
			UpdateStatBars(null);
			return;
		}

		unitName.Text = gridObject.Name;
		_unitIcon.Texture = gridObject.Thumbnail;
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
		foreach (GridStatBarUI statBarUI in _statBars)
		{
			statBarUI.SetPreviewCosts(null);
			statBarUI.SetupStatBar(gridObject);
		}
	}

	private void OnActionPreviewChanged(
		ActionDefinition action,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	)
	{
		foreach (GridStatBarUI statBarUI in _statBars)
		{
			statBarUI.SetPreviewCosts(costs);
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

	public override void _ExitTree()
	{
		GridObjectTeamHolder playerTeam = GridObjectManager.Instance?
			.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (playerTeam != null)
		{
			playerTeam.SelectedGridObjectChanged -= OnSelectedGridObjectChanged;
		}
		if (ActionManager.Instance != null)
		{
			ActionManager.Instance.ActionPreviewChanged -= OnActionPreviewChanged;
		}
		base._ExitTree();
	}
}
