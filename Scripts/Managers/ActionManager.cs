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

	protected override async Task _Setup()
	{
		await Task.CompletedTask;
	}

	protected override async Task _Execute()
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
						selectedGridObject.GridPositionData.GridCell,
						currentGridCell
					);
					return;
				}
			}

			ActionDefinition[] actions = selectedGridObject.ActionDefinitions
				.Where(action => action.GetIsAlwaysActive())
				.ToArray();

			foreach (var action in actions)
			{
				if (mouseButton.ButtonIndex == action.GetActionInput())
				{
					_ = RunTryTakeActionAsync(
						action,
						selectedGridObject,
						selectedGridObject.GridPositionData.GridCell,
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
		try
		{
			await TryTakeAction(action, gridObject, start, target, extraData);
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
		if (action is IItemActionDefinition itemActionDefinition)
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

		if (targetGridCell == null)
		{
			GD.Print("TryTakeAction: targetGridCell is null");
			return false;
		}

		if (action is IItemActionDefinition itemActionDefinition &&
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
	}

	// New API: only clear IsBusy if the completed action is the root (no parent)
	public void ActionCompleteCall(ActionDefinition actionDef, global::Action actionInst)
	{
		switch (actionDef)
		{
			case null:
				GD.Print("Action is null");
				return;
			case IItemActionDefinition itemActionDefinition:
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
			
			// Only manage the busy flag if it's the player's turn.
			// During the AI turn, the TurnManager is responsible for the busy state.
			if (TurnManager.Instance.CurrentTurn.team == Enums.UnitTeam.Player)
			{
				SetIsBusy(false);
			}

			if (actionDef == SelectedAction && !actionDef.GetRemainSelected())
			{
				SetSelectedAction(GridObjectManager.Instance
					.CurrentPlayerGridObject
					.ActionDefinitions
					.First());
			}
		}
	}


}