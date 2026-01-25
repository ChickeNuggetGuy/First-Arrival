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

	[Signal]
	public delegate void ActionCompletedEventHandler(ActionDefinition actionCompleted, ActionDefinition currentAction);
	
	
	public override string GetManagerName() =>  "ActionManager";
	
	
	protected override async Task _Setup(bool loadingData)
	{
		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		await Task.CompletedTask;
	}

	public override void _Input(InputEvent @event)
	{
		if (IsBusy) return;
		if(TurnManager.Instance.CurrentTurn.team != Enums.UnitTeam.Player) return;
		if (InputManager.Instance.MouseOverUI) return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			GridObject selectedGridObject = GridObjectManager.Instance
				.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject;
			if (selectedGridObject == null)
			{
				GD.Print("_Input: SelectedGridObject == null");
				return;
			}

			GridCell currentGridCell = InputManager.Instance.currentGridCell;
			if (currentGridCell == null)
			{
				GD.Print("_Input: CurrentGridCell == null");
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
			if(!selectedGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode))return;
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
			SetIsBusy(false);
		}
	}

	public void SetSelectedAction(
		ActionDefinition action,
		Dictionary<string, Variant> extraData = null
	)
	{
		SelectedAction = action;
		if (action is ItemActionDefinition itemActionDefinition)
		{
			if (extraData != null && extraData.ContainsKey("item"))
			{
				itemActionDefinition.Item = extraData["item"].As<Item>();
			}
		}

		GD.Print($"set selected action {SelectedAction.GetActionName()}");
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

		if (action is ItemActionDefinition itemActionDefinition &&
		    itemActionDefinition.Item == null)
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

		if (result)
		{
			GD.Print(
				$"action: {action.GetActionName()} can be taken, starting execution"
			);
			SetIsBusy(true);
			try
			{
				await action.InstantiateActionCall(
					gridObject,
					startingGridCell,
					targetGridCell,
					costs
				);
			}
			catch (Exception e)
			{
				GD.PushError(
					$"Exception during action '{action.GetActionName()}': {e}"
				);
				SetIsBusy(false);
			}

			return true;
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

	// New API: only clear IsBusy if the completed action is the root (no parent)
	public void ActionCompleteCall(ActionDefinition actionDef, global::Action actionInst)
	{
		if (actionDef?.parentGridObject != null)
		{
			var teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(actionDef.parentGridObject.Team);
			if (teamHolder != null)
			{
				teamHolder.UpdateGridObjects(actionDef, SelectedAction);
			}
		}
		
		EmitSignal(SignalName.ActionCompleted, actionDef, SelectedAction);
		switch (actionDef)
		{
			case null:
				GD.Print("Action is null");
				return;
			case ItemActionDefinition itemActionDefinition:
				if(!actionDef.GetRemainSelected())
					itemActionDefinition.Item = null;
				break;
			case MoveActionDefinition moveActionDefinition:
				if(!actionDef.GetRemainSelected())
					moveActionDefinition.path = null;
				break;
		}

		bool isRoot = actionInst == null || actionInst.Parent == null;

		if (isRoot)
		{
			GD.Print($"{actionDef.GetActionName()} complete");
			
			SetIsBusy(false);

			if (TurnManager.Instance.CurrentTurn.team == Enums.UnitTeam.Player)
			{
				if (actionDef == SelectedAction && !actionDef.GetRemainSelected())
				{
					if(!GridObjectManager.Instance.CurrentPlayerGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode))
					SetSelectedAction(gridObjectActionsNode
						.ActionDefinitions
						.First());
				}
			}
		}
	}

	#region manager Data
	public override void Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		base.Load(data);
		if(!HasLoadedData) return;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		return null;
	}
	#endregion

	public override void Deinitialize()
	{
		return;
	}
}