using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public abstract partial class Action
{
	protected GridObject parentGridObject;
	protected GridCell startingGridCell;
	protected GridCell targetGridCell;
	protected ActionDefinition parentActionDefinition;
	
	protected Dictionary<Enums.Stat, int> costs = new Dictionary<Enums.Stat, int>();

	public Action()
	{
		
	}
	public Action(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, 
		ActionDefinition parent, Dictionary<Enums.Stat, int> costs)
	{
		this.parentActionDefinition = parent;
		this.parentGridObject = parentGridObject;
		this.startingGridCell = startingGridCell;
		this.targetGridCell = targetGridCell;
			costs = costs;
	}


	public virtual async Task SetupCall()
	{
		if (this is ICompositeAction compositeAction)
		{
			if (compositeAction.SubActions != null)
			{
				compositeAction.SubActions.Clear();
			}
			else
			{
				compositeAction.SubActions = new List<Action>();
			}

		}
		await Setup();
	}

	protected abstract Task Setup();

	public virtual async Task ExecuteCall()
	{
		await SetupCall();
		if (this is ICompositeAction compositeAction)
		{
			foreach (Action action in compositeAction.SubActions)
			{
				await action.ExecuteCall();
			}

		}
		await Execute();
		await ActionCompleteCall();
	}
	
	protected abstract Task Execute();

	public async Task ActionCompleteCall()
	{
		await ActionComplete();

		foreach (var pair in costs)
		{
			if(!parentGridObject.TryGetStat(pair.Key, out var stat))
			{
				GD.Print("Stat not found");
				continue;
			}
			stat.RemoveValue((int)costs[pair.Key]);
		}
		ActionManager.Instance.ActionCompleteCall(parentActionDefinition);
	}
	protected abstract Task ActionComplete();
}
