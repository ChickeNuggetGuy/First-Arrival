using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.AIActions;

public class Rotate360ActionBase : ActionBase, ICompositeAction
{
	public ActionBase ParentActionBase { get; set; }
	public List<ActionBase> SubActions { get; set; }
	private bool _foundVisibleEnemy;
	
	public Rotate360ActionBase(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, 
		ActionDefinition parent,  Godot.Collections.Dictionary<Enums.Stat, int> costs) 
		: base(parentGridObject, startingGridCell, targetGridCell, parent, costs)
	{
	}

	protected override async Task Setup()
	{
		ParentActionBase = this;
		_foundVisibleEnemy = false;
		
		if(!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode))return;
		
		var rotateActionDefinition = gridObjectActionsNode.ActionDefinitions.FirstOrDefault(a => a is RotateActionDefinition) as RotateActionDefinition;
		if (rotateActionDefinition == null)
		{
			return;
		}

		var currentDirectionValue = (int)parentGridObject.GridPositionData.Direction;
		
		for (var i = 1; i <= 8; i++)
		{
			var nextDirectionValue = (currentDirectionValue - 1 + i) % 8 + 1;
			var nextDirection = (Enums.Direction)nextDirectionValue;
			
			// A rotation should not depend on a neighboring cell existing. Passing
			// the direction directly also allows a complete scan at map edges.
			var rotateAction = new RotateActionBase(
				parentGridObject,
				startingGridCell,
				startingGridCell,
				rotateActionDefinition,
				new Godot.Collections.Dictionary<Enums.Stat, int>(),
				nextDirection
			);
			
			AddSubAction(rotateAction);
		}

		await Task.CompletedTask;
	}

	protected override bool ShouldContinueAfterSubAction(ActionBase completedSubAction)
	{
		// RotateActionBase commits GridPositionData.Direction before this check.
		// Recalculate explicitly so the scan responds to each completed step.
		_foundVisibleEnemy = HasVisibleEnemy();
		return !_foundVisibleEnemy;
	}

	protected override Task Execute()
	{
		// Sub-actions handle the execution
		return Task.CompletedTask;
	}

	protected override Task ActionComplete()
	{
		GD.Print($"Rotate 360 found visible enemy: {_foundVisibleEnemy}");
		return Task.CompletedTask;
	}

	private bool HasVisibleEnemy()
	{
		if (!parentGridObject.TryGetGridObjectNode<GridObjectSight>(out var sight))
			return false;

		sight.CalculateSightArea();

		return sight.SeenGridObjects.Any(gridObject =>
			gridObject != null
			&& gridObject.IsActive
			&& !gridObject.scenery
			&& gridObject.Team != parentGridObject.Team
		);
	}
}
