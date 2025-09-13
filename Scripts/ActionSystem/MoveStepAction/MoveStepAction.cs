using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public class MoveStepAction : Action, ICompositeAction
{
  public Action ParentAction { get; set; }
  public List<Action> SubActions { get; set; }

  public MoveStepAction(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parent,
    Dictionary<Enums.Stat, int> costs
  )
    : base(parentGridObject, startingGridCell, targetGridCell, parent, costs)
  {
  }

  protected override async Task Setup()
  {
    ParentAction = this;

    // Use actual transform-facing, not cached direction state
    Enums.Direction currentDirection =
      RotationHelperFunctions.GetDirectionFromRotation3D(
        parentGridObject.Rotation.Y
      );

    Enums.Direction targetDirection =
      RotationHelperFunctions.GetDirectionBetweenCells(
        startingGridCell,
        targetGridCell
      );

    if (currentDirection != targetDirection)
    {
      // Not facing the correct direction, rotate first
      var rotateActionDefinition =
        parentGridObject.ActionDefinitions.FirstOrDefault(
          node => node is RotateActionDefinition
        ) as RotateActionDefinition;

      if (rotateActionDefinition == null)
      {
        await Task.CompletedTask;
        return;
      }
      
      Dictionary<Enums.Stat, int> rotateCosts = new();
      if (!rotateActionDefinition.TryBuildCostsOnly(
            parentGridObject,
            startingGridCell,
            targetGridCell,
            out rotateCosts,
            out _
          ))
      {
        int steps =
          RotationHelperFunctions.GetRotationStepsBetweenDirections(
            currentDirection,
            targetDirection
          );
        rotateCosts = new Dictionary<Enums.Stat, int>
        {
          { Enums.Stat.TimeUnits, Mathf.Abs(steps) * 1 },
          { Enums.Stat.Stamina, Mathf.Abs(steps) * 1 },
        };
      }

      var rotateAction =
        (RotateAction)rotateActionDefinition.InstantiateAction(
          parentGridObject,
          startingGridCell,
          targetGridCell,
          rotateCosts
        );

      AddSubAction(rotateAction);
    }

    await Task.CompletedTask;
  }

  protected override async Task Execute()
  {
    float distance = startingGridCell.worldCenter.DistanceTo(
      targetGridCell.worldCenter
    );
	
    if (distance < 0.0001f)
    {
      parentGridObject.Position = targetGridCell.worldCenter;
      await Task.CompletedTask;
      return;
    }

    Tween tween = parentGridObject.CreateTween();
    tween.SetTrans(Tween.TransitionType.Sine);
    tween.SetEase(Tween.EaseType.InOut);
    tween.TweenProperty(
      parentGridObject,
      "position",
      targetGridCell.worldCenter,
      0.5f
    );
    await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
  }

  protected override Task ActionComplete()
  {
    parentGridObject.GridPositionData.SetGridCell(targetGridCell);
    return Task.CompletedTask;
  }
}