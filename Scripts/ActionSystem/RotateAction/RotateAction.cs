using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class RotateAction : Action
{
  private Enums.Direction _targetDirection;

  private const float TurnSpeedDegPerSec = 540f;
  private const bool UseTween = true;

  public RotateAction(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parentAction,
    Godot.Collections.Dictionary<Enums.Stat, int> costs,
    Enums.Direction targetDirection
  ) : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
  {
    _targetDirection = targetDirection;
  }

  protected override Task Setup()
  {
    if (_targetDirection == Enums.Direction.None &&
        startingGridCell != null &&
        targetGridCell != null)
    {
      _targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(
        startingGridCell,
        targetGridCell
      );
    }

    return Task.CompletedTask;
  }
  
  public void AppendToTween(Tween tween, ref float currentYaw)
  {
    if (_targetDirection == Enums.Direction.None) return;

    float targetYawRad = RotationHelperFunctions.GetRotationRadians(_targetDirection);

    float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
    if (Mathf.Abs(delta) < 0.0001f)
      return;

    float finalYaw = currentYaw + delta;

    if (!UseTween)
    {
      tween.TweenCallback(Callable.From(() =>
      {
        var r = parentGridObject.Rotation;
        r.Y = finalYaw;
        parentGridObject.Rotation = r;
      }));
    }
    else
    {
      float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);

      var tw = tween.TweenProperty(parentGridObject, "rotation:y", finalYaw, duration);
      tw.SetTrans(Tween.TransitionType.Sine);
      tw.SetEase(Tween.EaseType.InOut);
    }

    tween.TweenCallback(Callable.From(() =>
    {
      parentGridObject.GridPositionData.SetDirection(_targetDirection);
    }));

    currentYaw = finalYaw;
  }
  
  protected override async Task Execute()
  {
	  if (_targetDirection == Enums.Direction.None) return;

	  float targetYawRad = RotationHelperFunctions.GetRotationRadians(_targetDirection);

	  // IMPORTANT: read mesh yaw, not parent yaw
	  float currentYaw = parentGridObject.Rotation.Y;
	  float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
	  float finalYaw = currentYaw + delta;

	  float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);
	  if (duration < 0.0001f)
	  {
		  var r = parentGridObject.Rotation;
		  r.Y = finalYaw;
		  parentGridObject.Rotation = r;
		  return;
	  }

	  Tween tween = parentGridObject.CreateTween();
	  tween.SetTrans(Tween.TransitionType.Sine);
	  tween.SetEase(Tween.EaseType.InOut);

	  tween.TweenProperty(parentGridObject, "rotation:y", finalYaw, duration);
	  await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
  }

  protected override Task ActionComplete()
  {
	  var facing = RotationHelperFunctions.GetDirectionFromRotation3D(
		  parentGridObject.Rotation.Y
	  );
	  parentGridObject.GridPositionData.SetDirection(facing);
	  return Task.CompletedTask;
  }
}