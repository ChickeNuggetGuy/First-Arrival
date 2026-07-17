using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class ActionManager : Manager<ActionManager>
{
	public ActionDefinition SelectedAction { get; private set; }
	public ActionBase CurrentAction { get; private set; }
	public bool LastActionWasInterruptedByNewEnemy { get; private set; }
	private GridCellHighlighter _cellHighlighter;

	private GridCell currentGridCell;

	private List<(ActionBase action, GridObject gridObject)> delayedActions = new();

	[Signal]
	public delegate void ActionCompletedEventHandler(ActionDefinition actionCompleted, ActionDefinition currentAction);
	[Signal]
	public delegate void ActionCanceledEventHandler(ActionDefinition actionCanceled, ActionDefinition currentAction);


	public override string GetManagerName() => "ActionManager";


	protected override async Task _Setup(bool loadingData)
	{
		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		_cellHighlighter = new GridCellHighlighter { Name = "ActionCellHighlighter" };
		AddChild(_cellHighlighter);

		GridObjectTeamHolder playerTeam = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);

		if (playerTeam != null)
		{
			playerTeam.SelectedGridObjectChanged += PlayerTeamOnSelectedGridObjectChanged;
		}
		await Task.CompletedTask;
	}

	private void PlayerTeamOnSelectedGridObjectChanged(GridObject gridObject)
	{
		if (gridObject == null)
		{
			SelectedAction = null;
			_cellHighlighter?.Clear();
			return;
		}

		gridObject.TryGetGridObjectNode<GridObjectActions>(out GridObjectActions gridObjectActions);
		if (gridObjectActions != null)
		{
			SetSelectedAction(gridObjectActions.ActionDefinitions[0]);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (
			@event is InputEventMouseButton
			{
				Pressed: true,
				ButtonIndex: MouseButton.Right
			}
			&& IsBusy
			&& CurrentAction != null
			&& TurnManager.Instance.CurrentTurn.team == Enums.UnitTeam.Player
		)
		{
			GetViewport().SetInputAsHandled();
			_ = CancelCurrentAction();
			return;
		}

		if (IsBusy) return;
		if (TurnManager.Instance.CurrentTurn.team != Enums.UnitTeam.Player) return;
		if (BattleInputManager.Instance.MouseOverUI) return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			GridObject selectedGridObject = GridObjectManager.Instance
				.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject;
			if (selectedGridObject == null)
			{
				GD.Print("_Input: SelectedGridObject == null");
				return;
			}

			GridCell currentGridCell = BattleInputManager.Instance.currentGridCell;
			if (currentGridCell == null)
			{
				return;
			}

			if (SelectedAction != null)
			{
				MouseButton actionInput = SelectedAction.GetActionInput();

				if (mouseButton.ButtonIndex == actionInput)
				{
					_ = RunTryTakeActionAsync(
						SelectedAction,
						selectedGridObject,
						selectedGridObject.GridPositionData.AnchorCell,
						currentGridCell
					);
					return;
				}
			}

			if (!selectedGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode)) return;
			ActionDefinition[] actions = gridObjectActionsNode.ActionDefinitions
				.Where(action => action.GetIsAlwaysActive())
				.ToArray();

			foreach (var action in actions)
			{
				if (mouseButton.ButtonIndex == action.GetActionInput())
				{
					_ = RunTryTakeActionAsync(
						action,
						selectedGridObject,
						selectedGridObject.GridPositionData.AnchorCell,
						currentGridCell
					);
					return;
				}
			}
		}
	}

	private async Task RunTryTakeActionAsync(
		ActionDefinition action,
		GridObject gridObject,
		GridCell start,
		GridCell target,
		Dictionary<string, Variant> extraData = null
	)
	{
		LastActionWasInterruptedByNewEnemy = false;

		if (action == null)
		{
			GD.Print("ActionManager.RunTryTakeActionAsync: action is null");
			return;
		}

		if (gridObject == null)
		{
			GD.Print("ActionManager.RunTryTakeActionAsync: gridObject is null");
			return;
		}

		if (start == null || target == null)
		{
			GD.Print("ActionManager.RunTryTakeActionAsync: start or target is null");
			return;
		}

		try
		{
			await TryTakeAction(action, gridObject, start, target, null);
		}
		catch (Exception e)
		{
			GD.PushError($"TryTakeAction failed: {e}");
			CurrentAction = null;
			SetIsBusy(false);
		}
	}

	public async Task<bool> CancelCurrentAction()
	{
		ActionBase action = CurrentAction;
		if (!IsBusy || action == null)
			return false;

		try
		{
			await action.CancelCall();
			return true;
		}
		catch (Exception e)
		{
			GD.PushError($"Failed to cancel current action: {e}");
			return false;
		}
	}

	public void SetSelectedAction(
		ActionDefinition action,
		Dictionary<string, Variant> extraData = null
	)
	{
		SelectedAction = action;

		if (action == null)
		{
			RefreshValidCellHighlights();
			return;
		}

		if (action is ItemActionDefinition itemActionDefinition)
		{
			if (extraData != null && extraData.ContainsKey("item"))
			{
				GD.Print("Set item");
				itemActionDefinition.Item = extraData["item"].As<Item>();
			}
		}

		RefreshValidCellHighlights();

		if(DebugMode)
			GD.Print($"set selected action to {SelectedAction.GetActionName()}");
	}

	private void RefreshValidCellHighlights()
	{
		GridObject selectedGridObject = GridObjectManager.Instance?
			.GetGridObjectTeamHolder(Enums.UnitTeam.Player)?.CurrentGridObject;

		if (SelectedAction == null || selectedGridObject?.GridPositionData?.AnchorCell == null)
		{
			_cellHighlighter?.Clear();
			return;
		}

		SelectedAction.UpdateValidGridCells(
			selectedGridObject,
			selectedGridObject.GridPositionData.AnchorCell
		);
		_cellHighlighter?.ShowCells(SelectedAction.ValidGridCells);
	}

	public async Task<bool> TryTakeAction(
		ActionDefinition action,
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		Dictionary<string, Variant> extraData = null
	)
	{
		if (action == null)
		{
			GD.Print("TryTakeAction: action is null");
			return false;
		}

		if (gridObject == null)
		{
			GD.Print("TryTakeAction: gridObject is null");
			return false;
		}

		if (startingGridCell == null)
		{
			GD.Print("TryTakeAction: startingGridCell is null");
			return false;
		}

		if (targetGridCell == null)
		{
			GD.Print("TryTakeAction: targetGridCell is null");
			return false;
		}

		if (action is ItemActionDefinition { Item: null })
		{
			GD.Print("tryTakeAction: Item is null");
			return false;
		}

		var result = action.CanTakeAction(
			gridObject,
			startingGridCell,
			targetGridCell,
			out var costs,
			out string reason
		);

		if (action.confirmClick)
		{
			if (currentGridCell == null)
			{
				currentGridCell = targetGridCell;
				return false;
			}
			else if (targetGridCell == currentGridCell)
			{
				currentGridCell = null;
			}
			else
			{
				currentGridCell = targetGridCell;
				return false;
			}
		}

		if (result)
		{
			GD.Print($"action: {action.GetActionName()} can be taken, starting execution");
			_cellHighlighter?.Clear();
			SetIsBusy(true);
			try
			{
				var actionInstance = action.InstantiateAction(
					gridObject,
					startingGridCell,
					targetGridCell,
					costs
				);
				CurrentAction = actionInstance;

				if (actionInstance is IDelayedAction delayedAction)
				{
					await actionInstance.ExecuteCall();
					RegisterDelayedAction(actionInstance, gridObject);
				}
				else
				{
					await actionInstance.ExecuteCall();
				}

				// An interrupted composite action may have completed one or more
				// child actions before a newly revealed hostile stopped it.
				LastActionWasInterruptedByNewEnemy = actionInstance.WasInterruptedByNewEnemy;
				return !LastActionWasInterruptedByNewEnemy;
			}
			catch (Exception e)
			{
				GD.PushError($"Exception during action '{action.GetActionName()}': {e}");
				CurrentAction = null;
				SetIsBusy(false);
				return false;
			}

		}
		else
		{
			GD.Print(reason);
			return false;
		}
	}

	public void ActionCompleteCall(ActionDefinition actionDef)
	{
		ActionCompleteCall(actionDef, null);
		EmitSignal(SignalName.ActionCompleted, actionDef, SelectedAction);
	}


	public void ProcessDelayedActions()
	{
		for (int i = delayedActions.Count - 1; i >= 0; i--)
		{
			var (action, gridObject) = delayedActions[i];

			if (action is IDelayedAction delayed)
			{
				bool shouldRemove = delayed.OnTurnTick();

				if (shouldRemove)
				{
					delayedActions.RemoveAt(i);
					delayed.OnDelayComplete();
				}
			}
		}
	}

	public void RegisterDelayedAction(ActionBase actionBase, GridObject gridObject)
	{
		delayedActions.Add((actionBase, gridObject));
	}

	public void ActionCompleteCall(ActionDefinition actionDef, global::ActionBase actionBaseInst)
	{
		GridObject actingObject = actionBaseInst?.ActingGridObject ?? actionDef?.parentGridObject;
		if (actingObject != null)
		{
			var teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(actingObject.Team);
			if (teamHolder != null)
			{
				var visibleEnemiesBeforeAction = GetVisibleEnemies(teamHolder, actingObject.Team);
				teamHolder.UpdateGridObjects(actionDef, SelectedAction);

				if (GetVisibleEnemies(teamHolder, actingObject.Team)
					.Any(enemy => !visibleEnemiesBeforeAction.Contains(enemy)))
				{
					GD.Print($"{actingObject.Name} revealed a new enemy; interrupting the current action.");
					CurrentAction?.RequestVisibilityInterrupt();
				}
			}
		}

		EmitSignal(SignalName.ActionCompleted, actionDef, SelectedAction);
		switch (actionDef)
		{
			case null:
				GD.Print("Action is null");
				return;
			case ItemActionDefinition itemActionDefinition:
				if (!actionDef.GetRemainSelected())
					itemActionDefinition.Item = null;
				break;
			case MoveActionDefinition moveActionDefinition:
				if (!actionDef.GetRemainSelected())
					moveActionDefinition.path = null;
				break;
		}

		bool isRoot = actionBaseInst == null || actionBaseInst.Parent == null;

		if (isRoot)
		{
			GD.Print($"{actionDef.GetActionName()} complete");

			if (actionBaseInst == null || ReferenceEquals(CurrentAction, actionBaseInst))
				CurrentAction = null;
			SetIsBusy(false);

			if (TurnManager.Instance.CurrentTurn.team == Enums.UnitTeam.Player)
			{
				if (actionDef == SelectedAction && !actionDef.GetRemainSelected())
				{
					if (!GridObjectManager.Instance.CurrentPlayerGridObject.TryGetGridObjectNode<GridObjectActions>(
						    out var gridObjectActionsNode))
						SetSelectedAction(gridObjectActionsNode
							.ActionDefinitions
							.First());
				}
			}

			RefreshValidCellHighlights();
		}
	}

	private static HashSet<GridObject> GetVisibleEnemies(
		GridObjectTeamHolder viewerTeam,
		Enums.UnitTeam viewerTeamId
	)
	{
		var visibleEnemies = new HashSet<GridObject>();
		var manager = GridObjectManager.Instance;
		if (viewerTeam == null || manager == null)
			return visibleEnemies;

		foreach (var teamHolder in manager.GetGridObjectTeamHolders().Values)
		{
			if (teamHolder == null || teamHolder.Team == viewerTeamId)
				continue;

			foreach (var gridObject in teamHolder.GridObjects[Enums.GridObjectState.Active])
			{
				GridCell anchorCell = gridObject?.GridPositionData?.AnchorCell;
				if (gridObject != null && gridObject.IsActive && !gridObject.scenery &&
					anchorCell != null && viewerTeam.TeamVisibleCells.Contains(anchorCell))
					visibleEnemies.Add(gridObject);
			}
		}

		return visibleEnemies;
	}

	public void ActionCanceledCall(ActionDefinition actionDef, global::ActionBase actionBaseInst)
	{
		if (actionBaseInst?.Parent != null)
			return;

		if (CurrentAction != null && !ReferenceEquals(CurrentAction, actionBaseInst))
			return;

		CurrentAction = null;

		switch (actionDef)
		{
			case ItemActionDefinition itemActionDefinition:
				if (!actionDef.GetRemainSelected())
					itemActionDefinition.Item = null;
				break;
			case MoveActionDefinition moveActionDefinition:
				moveActionDefinition.path = null;
				break;
		}

		GD.Print($"{actionDef?.GetActionName() ?? "Action"} canceled");
		SetIsBusy(false);
		RefreshValidCellHighlights();
		EmitSignal(SignalName.ActionCanceled, actionDef, SelectedAction);
	}

	#region manager Data

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!HasLoadedData)  return Task.CompletedTask;
		return Task.CompletedTask;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		return null;
	}

	#endregion

	public override void Deinitialize()
	{
		return;
	}
}
