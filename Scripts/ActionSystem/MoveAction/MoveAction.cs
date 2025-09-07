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
	public MoveAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,
		(Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data) : base(parentGridObject, startingGridCell, targetGridCell, data)
	{
		if (data.extraData.ContainsKey("path"))
		{
			GridSystem gridSystem = GridSystem.Instance;
			foreach (Vector3I gridCoords in (Godot.Collections.Array)data.extraData["path"])
			{
				path.Add(gridSystem.GetGridCell(gridCoords));
			}
		}
		else
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
				startingGridCell,
				nextGridCell,
				(costs, null)) as MoveStepAction;
			SubActions.Add(moveStepAction);
		}
	}

	protected override async Task Execute()
	{
		GD.Print("MoveAction");
		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}
