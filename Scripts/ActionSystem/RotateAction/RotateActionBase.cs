using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class RotateActionBase : ActionBase
{
  private Enums.Direction _targetDirection;

  private const float TurnSpeedDegPerSec = 540f;
  private const bool UseTween = true;

  public RotateActionBase(
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
    float finalYaw = currentYaw + delta;

    if (Mathf.Abs(delta) >= 0.0001f && !UseTween)
    {
	    tween.TweenCallback(Callable.From(() =>
      {
        var r = parentGridObject.visualMesh.Rotation;
        r.Y = finalYaw;
        parentGridObject.visualMesh.Rotation = r;
      }));
    }
    else if (Mathf.Abs(delta) >= 0.0001f)
    {
      float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);

      var tw = tween.TweenProperty(parentGridObject.visualMesh, "rotation:y", finalYaw, duration);
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

	  float currentYaw = parentGridObject.visualMesh.Rotation.Y;
	  float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
	  float finalYaw = currentYaw + delta;

	  float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);
	  if (duration < 0.0001f)
	  {
		  var r = parentGridObject.visualMesh.Rotation;
		  r.Y = finalYaw;
		  parentGridObject.visualMesh.Rotation = r;
		  return;
	  }

	  Tween tween = parentGridObject.visualMesh.CreateTween();
	  tween.SetTrans(Tween.TransitionType.Sine);
	  tween.SetEase(Tween.EaseType.InOut);

	  tween.TweenProperty(parentGridObject.visualMesh, "rotation:y", finalYaw, duration);
	  await parentGridObject.visualMesh.ToSignal(tween, Tween.SignalName.Finished);
  }

  protected override Task ActionComplete()
  {
	  // The action's target is the source of truth. Deriving this from a
	  // transform reintroduces rounding/model-forward-offset errors.
	  parentGridObject.GridPositionData.SetDirection(_targetDirection);
	  return Task.CompletedTask;
  }
}
