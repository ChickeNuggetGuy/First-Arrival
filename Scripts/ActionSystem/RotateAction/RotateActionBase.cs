using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class RotateActionBase : ActionBase
{
  private Enums.Direction _targetDirection;
  private Tween _rotationTween;
  private float _startingYaw;
  private bool _rotationStarted;

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

    ApplyAnimationSpeed(tween);

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
	  _startingYaw = currentYaw;
	  _rotationStarted = true;
	  float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
	  float finalYaw = currentYaw + delta;

	  float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);
	  if (!ShouldAnimate() || duration < 0.0001f)
	  {
		  var r = parentGridObject.visualMesh.Rotation;
		  r.Y = finalYaw;
		  parentGridObject.visualMesh.Rotation = r;
		  return;
	  }

	  _rotationTween = ApplyAnimationSpeed(parentGridObject.visualMesh.CreateTween());
	  _rotationTween.SetTrans(Tween.TransitionType.Sine);
	  _rotationTween.SetEase(Tween.EaseType.InOut);

	  _rotationTween.TweenProperty(parentGridObject.visualMesh, "rotation:y", finalYaw, duration);
	  await WaitForTween(_rotationTween);
	  _rotationTween = null;
  }

  protected override Task ActionComplete()
  {
	  // The action's target is the source of truth. Deriving this from a
	  // transform reintroduces rounding/model-forward-offset errors.
	  parentGridObject.GridPositionData.SetDirection(_targetDirection);
	  return Task.CompletedTask;
  }

  protected override Task ActionCanceled()
  {
	  if (_rotationTween != null && GodotObject.IsInstanceValid(_rotationTween))
		  _rotationTween.Kill();
	  _rotationTween = null;

	  if (
		  _rotationStarted
		  && parentGridObject?.visualMesh != null
		  && GodotObject.IsInstanceValid(parentGridObject.visualMesh)
	  )
	  {
		  Vector3 rotation = parentGridObject.visualMesh.Rotation;
		  rotation.Y = _startingYaw;
		  parentGridObject.visualMesh.Rotation = rotation;
	  }

	  return Task.CompletedTask;
  }
}
