using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

public partial class RotateAction : Action
{
  private Enums.Direction _targetDirection;

  // Tween settings
  private const float TurnSpeedDegPerSec = 540f;
  private const bool UseTween = true;

  public RotateAction(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parentAction,
    Dictionary<Enums.Stat, int> costs,
    Enums.Direction targetDirection
  )
    : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
  {
    _targetDirection = targetDirection;
  }

  protected override async Task Setup()
  {
    if (_targetDirection == Enums.Direction.None && startingGridCell != null && targetGridCell != null)
    {
      _targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(
        startingGridCell,
        targetGridCell
      );
    }
    await Task.CompletedTask;
  }

  protected override async Task Execute()
  {
	  GD.Print($"RotateAction, starting direction: {parentGridObject.GridPositionData.Direction}. ");
    if (_targetDirection == Enums.Direction.None)
    {
      await Task.CompletedTask;
      return;
    }

    // Correct yaw for the target direction (respects NorthIsPlusZ and ModelForwardYawOffsetDeg).
    float targetYawRad = RotationHelperFunctions.GetRotationRadians(_targetDirection);

    // Rotate shortest path
    float currentYaw = parentGridObject.Rotation.Y;
    float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
    float finalYaw = currentYaw + delta;

    if (!UseTween)
    {
      var r = parentGridObject.Rotation;
      r.Y = finalYaw;
      parentGridObject.Rotation = r;
      await Task.CompletedTask;
      return;
    }

    float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);
    if (duration < 0.0001f)
    {
      var r = parentGridObject.Rotation;
      r.Y = finalYaw;
      parentGridObject.Rotation = r;
      await Task.CompletedTask;
      return;
    }

    Tween tween = parentGridObject.CreateTween();
    tween.SetTrans(Tween.TransitionType.Sine);
    tween.SetEase(Tween.EaseType.InOut);
    tween.TweenProperty(parentGridObject, "rotation:y", finalYaw, duration);
    await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
  }

  protected override async Task ActionComplete()
  {
    // Snap stored facing to actual transform to avoid drift/rounding issues
    var facing = RotationHelperFunctions.GetDirectionFromRotation3D(parentGridObject.Rotation.Y);
    parentGridObject.GridPositionData.SetDirection(facing);
    await Task.CompletedTask;
  }
}