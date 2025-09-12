using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;


public partial class MoveAction : Action, ICompositeAction
{
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }
	
	List<GridCell> path = new List<GridCell>();
	public MoveAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,ActionDefinition parent,
		Dictionary<Enums.Stat, int> costs) : base(parentGridObject, startingGridCell,
		targetGridCell,parent, costs)
	{
		if (parent is MoveActionDefinition moveActionDefinition)
		{
			path = moveActionDefinition.path;
		}
		if(path.Count == 0)
		{
			GD.Print("Path not found!");
		}
	}

	protected override async Task Setup()
	{
		ParentAction = this;
		if (path == null || path.Count == 0) return;

		MoveStepActionDefinition moveStepActionDefinition =
			parentGridObject.ActionDefinitions.FirstOrDefault(a => a is MoveStepActionDefinition) as MoveStepActionDefinition;
		
		if (moveStepActionDefinition == null) return;
		for (var index = 0; index < path.Count; index++)
		{
			var gridCell = path[index];
			if (index +1 >= path.Count) continue;
			GridCell nextGridCell = path[index + 1];

			MoveStepAction moveStepAction = moveStepActionDefinition.InstantiateAction(parentGridObject,
				startingGridCell, nextGridCell, costs) as MoveStepAction;
			SubActions.Add(moveStepAction);
		}
	}

	protected override async Task Execute()
	{
		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}
