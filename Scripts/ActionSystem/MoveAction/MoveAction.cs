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
		Godot.Collections.Dictionary<Enums.Stat, int> costs
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

		if (!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions)) return;

		var moveStepActionDefinition =
			gridObjectActions.ActionDefinitions.FirstOrDefault(a => a is MoveStepActionDefinition
			) as MoveStepActionDefinition;

		if (moveStepActionDefinition == null)
			return;
		
		var facing = RotationHelperFunctions.GetDirectionFromRotation3D(
			parentGridObject.Rotation.Y
		);

		// Build step actions using the correct cell for each step
		for (int i = 0; i < path.Count - 1; i++)
		{
			GridCell stepStart = path[i];
			GridCell stepEnd = path[i + 1];

			moveStepActionDefinition.TryBuildCostsOnly(parentGridObject, stepStart, stepEnd, out var stepCosts, out _);

			var moveStepAction =
				moveStepActionDefinition.InstantiateAction(
					parentGridObject,
					stepStart,
					stepEnd,
					stepCosts
				) as MoveStepAction;

			AddSubAction(moveStepAction);
		}

		costs.Clear();
		await Task.CompletedTask;
	}


	protected override async Task Execute()
	{
		await Task.CompletedTask;
	}

	private static Vector2 GetBlendForDirection(Enums.Direction dir)
	{
		if (dir is Enums.Direction.North or Enums.Direction.South or
		    Enums.Direction.East or Enums.Direction.West)
			return new Vector2(0, 0);

		if (dir is Enums.Direction.NorthEast or Enums.Direction.SouthEast)
			return new Vector2(1, 0);

		if (dir is Enums.Direction.NorthWest or Enums.Direction.SouthWest)
			return new Vector2(-1, 0);

		return Vector2.Zero;
	}

	protected override async Task ActionComplete()
	{
		parentGridObject.GridPositionData.SetGridCell(targetGridCell);
		await Task.CompletedTask;
	}
}