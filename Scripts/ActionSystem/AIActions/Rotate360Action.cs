using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.ActionSystem.AIActions;

public class Rotate360Action : Action, ICompositeAction
{
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }
	
	public Rotate360Action(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, ActionDefinition parent, Dictionary<Enums.Stat, int> costs) : base(parentGridObject, startingGridCell, targetGridCell, parent, costs)
	{
	}

	protected override async Task Setup()
	{
		ParentAction = this;

		var rotateActionDefinition = parentGridObject.ActionDefinitions.FirstOrDefault(a => a is RotateActionDefinition) as RotateActionDefinition;
		if (rotateActionDefinition == null)
		{
			return;
		}

		var currentDirectionValue = (int)parentGridObject.GridPositionData.Direction;
		
		for (var i = 1; i <= 8; i++)
		{
			var nextDirectionValue = (currentDirectionValue - 1 + i) % 8 + 1;
			var nextDirection = (Enums.Direction)nextDirectionValue;
			
			var nextCell = Managers.GridSystem.Instance.GetCellInDirection(startingGridCell, nextDirection);
			if (nextCell == null)
			{
				// If there's no cell in that direction (e.g., edge of the map), we can't create a standard RotateAction.
				// For now, we'll skip this step, though it means not a full 360 turn will be performed.
				// A better implementation might involve a RotateAction that can take a direction directly.
				continue;
			}
			
			var rotateAction = rotateActionDefinition.InstantiateAction(
				parentGridObject,
				startingGridCell,
				nextCell,
				new Dictionary<Enums.Stat, int>() // Costs are handled by the parent composite action
			);
			
			AddSubAction(rotateAction);
		}

		await Task.CompletedTask;
	}

	protected override Task Execute()
	{
		// Sub-actions handle the execution
		return Task.CompletedTask;
	}

	protected override Task ActionComplete()
	{
		return Task.CompletedTask;
	}
}