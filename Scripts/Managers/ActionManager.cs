using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class ActionManager : Manager<ActionManager>
{
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
		base._UnhandledInput(@event);
		if (InputManager.Instance.MouseOverUI) return;
		
		if (@event is InputEventMouseButton  mouseButton)
		{
			if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
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
				
				 _ = TryTakeAction(SelectedAction,selectedGridObject, selectedGridObject.GridPositionData.GridCell, currentGridCell);
			}
		}
	}
	
	public void SetSelectedAction(ActionDefinition action, Dictionary<string, Variant> extraData = null)
	{
		//Clear prevous Selected Action if there is onw
		if (SelectedAction != null)
		{
			SelectedAction.extraData.Clear();
		}
		
		SelectedAction = action;
		if (extraData != null)
		{
			SelectedAction.extraData = extraData;
		}
		GD.Print($"set selected action {SelectedAction.GetActionName()}");
	}

	public async Task TryTakeAction(ActionDefinition action, GridObject gridObject, GridCell startingGridCell,
		GridCell targetGridCell, Dictionary<string, Variant> extraData = null)
	{
		if (action == null)
		{
			GD.Print("TryTakeAction: action is null");
			return ;
		}
		
		if (gridObject == null)
		{
			GD.Print("TryTakeAction: gridObject is null");
			return ;
		}
		
		if (targetGridCell == null)
		{
			GD.Print("TryTakeAction: targetGridCell is null");
			return ;
		}

		if (action.extraData != null && action.extraData.Count > 0)
		{	if (extraData == null)
			{
				extraData = action.extraData;
			}
			else
			{
				foreach (var kvp in action.extraData)
				{
					extraData.Add(kvp.Key, kvp.Value);
				}
			}
		}
		if (SelectedAction is ItemActionDefinition itemActionDefinition)
		{
			if (extraData == null)
			{
				GD.Print("tryTakeAction: extraData is null");
				return ;
			}
			if(!extraData.ContainsKey("item"))
			{
				GD.Print("tryTakeAction: item is null");
				return ;
			}
		}

		var result = SelectedAction.CanTakeAction(gridObject, startingGridCell, targetGridCell, extraData,
			out var data);

		if (result == true)
		{
			GD.Print($"Time units {data.costs[Enums.Stat.TimeUnits]} , Stamina {data.costs[Enums.Stat.Stamina]}");
			await SelectedAction.InstantiateActionCall(gridObject, startingGridCell, targetGridCell, (data.costs, data.extraData));
			return;
		}
		else
		{
			GD.Print(data.reason);
			return ;
		}
	}
}
