using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class RotateActionBase : ActionBase
{
  private Enums.Direction _targetDirection;
  private Tween _rotationTween;
  private float _lastCommittedYaw;
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

	  Enums.Direction currentDirection = parentGridObject.GridPositionData.Direction;
	  int rotationSteps = RotationHelperFunctions.GetRotationStepsBetweenDirections(
		  currentDirection,
		  _targetDirection
	  );
	  float currentYaw = parentGridObject.visualMesh.Rotation.Y;
	  _lastCommittedYaw = currentYaw;
	  _rotationStarted = true;

	  // Commit every 45-degree heading crossed by the turn. DirectionChanged
	  // recalculates team visibility, allowing intermediate sight cones to add
	  // their cells to the explored set instead of jumping straight to the end.
	  if (rotationSteps != 0)
	  {
		  bool clockwise = rotationSteps > 0;
		  for (int step = 0; step < Mathf.Abs(rotationSteps); step++)
		  {
			  Enums.Direction nextDirection =
				  RotationHelperFunctions.GetNextDirection(currentDirection, clockwise);
			  if (!await RotateToDirection(nextDirection, currentYaw)) return;

			  parentGridObject.GridPositionData.SetDirection(nextDirection);
			  currentDirection = nextDirection;
			  currentYaw = parentGridObject.visualMesh.Rotation.Y;
			  _lastCommittedYaw = currentYaw;
		  }

		  return;
	  }

	  // Invalid/unknown starting directions cannot be stepped through, but the
	  // requested final direction should still be honored.
	  await RotateToDirection(_targetDirection, currentYaw);
  }

  private async Task<bool> RotateToDirection(
	  Enums.Direction direction,
	  float currentYaw
  )
  {
	  float targetYawRad = RotationHelperFunctions.GetRotationRadians(direction);
	  float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
	  float finalYaw = currentYaw + delta;
	  float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);

	  if (!ShouldAnimate() || duration < 0.0001f)
	  {
		  var rotation = parentGridObject.visualMesh.Rotation;
		  rotation.Y = finalYaw;
		  parentGridObject.visualMesh.Rotation = rotation;
		  return !IsCancellationRequested;
	  }

	  _rotationTween = ApplyAnimationSpeed(parentGridObject.visualMesh.CreateTween());
	  _rotationTween.SetTrans(Tween.TransitionType.Sine);
	  _rotationTween.SetEase(Tween.EaseType.InOut);
	  _rotationTween.TweenProperty(
		  parentGridObject.visualMesh,
		  "rotation:y",
		  finalYaw,
		  duration
	  );

	  bool completed = await WaitForTween(_rotationTween);
	  _rotationTween = null;
	  return completed;
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
		  // Keep any fully completed intermediate steps and discard only the
		  // partially animated step that was interrupted.
		  rotation.Y = _lastCommittedYaw;
		  parentGridObject.visualMesh.Rotation = rotation;
	  }

	  return Task.CompletedTask;
  }
}
