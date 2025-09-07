
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public class MoveStepAction : Action, ICompositeAction
{
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }
	public MoveStepAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, (Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data) : base(parentGridObject, startingGridCell, targetGridCell, data)
	{
	}

	protected override async Task Setup()
	{
		ParentAction = this;
		Enums.Direction currentDirection = parentGridObject.GridPositionData.Direction;
		Enums.Direction targetDirection =  RotationHelperFunctions.GetDirectionBetweenCells(startingGridCell, targetGridCell);

		if (currentDirection != targetDirection)
		{
			//Not facing the corret direction, rotate first
			RotateActionDefinition rotateActionDefinition = parentGridObject.ActionDefinitions.FirstOrDefault(node => 
				node is RotateActionDefinition) as RotateActionDefinition;
			
			if (rotateActionDefinition == null) return;

			RotateAction rotateAction =
				(RotateAction)rotateActionDefinition.InstantiateAction(parentGridObject, startingGridCell,
					targetGridCell,(costs,null));
			SubActions.Add(rotateAction);
						 
			
		}
	}

	protected override async Task Execute()
	{
		float distance = startingGridCell.worldCenter.DistanceTo(targetGridCell.worldCenter);
		float duration = Mathf.Abs(distance) / Mathf.DegToRad(0.3f);
		if (duration < 0.0001f)
		{
			var r = parentGridObject.Position = targetGridCell.worldCenter;
			await Task.CompletedTask;
			return;
		}

		Tween tween = parentGridObject.CreateTween();
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(parentGridObject, "position", targetGridCell.worldCenter, 0.5f);
		await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
	}

	protected override async Task ActionComplete()
	{
		parentGridObject.GridPositionData.SetGridCell(targetGridCell);
	}
}