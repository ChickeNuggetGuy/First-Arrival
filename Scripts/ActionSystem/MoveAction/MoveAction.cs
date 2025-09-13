using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class MoveAction : Action, ICompositeAction
{
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }

	private List<GridCell> path = new List<GridCell>();

	public MoveAction(
		GridObject parentGridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		ActionDefinition parent,
		Dictionary<Enums.Stat, int> costs
	)
		: base(
			parentGridObject,
			startingGridCell,
			targetGridCell,
			parent,
			costs
		)
	{
		if (parent is MoveActionDefinition moveActionDefinition)
		{
			path = moveActionDefinition.path;
		}
		if (path.Count == 0)
		{
			GD.Print("Path not found!");
		}
	}

	protected override async Task Setup()
	{
		ParentAction = this;
		if (path == null || path.Count == 0)
			return;

		var moveStepActionDefinition =
			parentGridObject.ActionDefinitions.FirstOrDefault(
				a => a is MoveStepActionDefinition
			) as MoveStepActionDefinition;

		if (moveStepActionDefinition == null)
			return;

		// Build step actions using the correct cell for each step
		for (int i = 0; i < path.Count - 1; i++)
		{
			GridCell stepStart = path[i];
			GridCell stepEnd = path[i + 1];

			var moveStepAction =
				moveStepActionDefinition.InstantiateAction(
					parentGridObject,
					stepStart,
					stepEnd,
					costs
				) as MoveStepAction;

			AddSubAction(moveStepAction);
		}

		await Task.CompletedTask;
	}

	protected override async Task Execute()
	{
		await Task.CompletedTask;
	}

	protected override async Task ActionComplete()
	{
		parentGridObject.GridPositionData.SetGridCell(targetGridCell);
		await Task.CompletedTask;
	}
}