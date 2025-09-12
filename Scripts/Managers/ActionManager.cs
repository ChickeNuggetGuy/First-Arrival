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
	public bool IsBusy { get; private set; }
	public ActionDefinition SelectedAction { get; private set; }

	protected override async Task _Setup()
	{
		return;
	}

	protected override async Task _Execute()
	{
		return;
	}

	public override void _Input(InputEvent @event)
	{
		if (IsBusy) return;
		base._UnhandledInput(@event);
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
					if (TryTakeAction(SelectedAction, selectedGridObject, selectedGridObject.GridPositionData.GridCell,
						    currentGridCell).Result)
					{
						return;
					}
				}
			}

			ActionDefinition[] actions = selectedGridObject.ActionDefinitions
				.Where(action => action.GetIsAlwaysActive()).ToArray();
			foreach (var action in actions)
			{
				if (mouseButton.ButtonIndex == action.GetActionInput())
				{
					TryTakeAction(action, selectedGridObject, selectedGridObject.GridPositionData.GridCell,
						currentGridCell);
				}
			}
		}
	}

	public void SetSelectedAction(ActionDefinition action, Dictionary<string, Variant> extraData = null)
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

	public async Task<bool> TryTakeAction(ActionDefinition action, GridObject gridObject, GridCell startingGridCell,
		GridCell targetGridCell, Dictionary<string, Variant> extraData = null)
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

		if (action is IItemActionDefinition itemActionDefinition && itemActionDefinition.Item == null)
		{
			GD.Print("tryTakeAction: Item is null");
			return false;
		}

		var result = action.CanTakeAction(gridObject, startingGridCell, targetGridCell, out var costs,
			out string reason);

		if (result == true)
		{
			GD.Print($"action: {action.GetActionName()} can be taken, starting execution");
			SetIsBusy(true);
			await action.InstantiateActionCall(gridObject, startingGridCell, targetGridCell, costs);
			return true;
		}
		else
		{
			GD.Print(reason);
			return false;
		}
	}

	public void ActionCompleteCall(ActionDefinition action)
	{
		GD.Print("action complete");

		switch (action)
		{
			case null:
				GD.Print("Action is null");
				return;
			case IItemActionDefinition itemActionDefinition:
				itemActionDefinition.Item = null;
				break;
			case MoveActionDefinition moveActionDefinition:
				moveActionDefinition.path = null;
				break;
		}

		if (action.GetIsAlwaysActive())
			SetIsBusy(false);

		if (action != SelectedAction) return;


		if (!action.remainSelected)
		{
			SetSelectedAction(GridObjectManager.Instance.CurrentPlayerGridObject.ActionDefinitions.First());
		}
	}

	public void SetIsBusy(bool isBusy)
	{
		IsBusy = isBusy;
		GD.Print($"ActionManager: SetIsBusy: {isBusy} ");
	}
}