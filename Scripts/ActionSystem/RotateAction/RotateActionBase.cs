using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class RotateActionBase : ActionBase
{
  private Enums.Direction _targetDirection;
  private Tween _rotationTween;
  private Quaternion _lastCommittedRotation;
  private bool _rotationStarted;
  private bool _rotationBlocked;

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
	Quaternion startRotation = new Quaternion(Vector3.Up, currentYaw).Normalized();
	Quaternion targetRotation = new Quaternion(Vector3.Up, finalYaw).Normalized();

    if (Mathf.Abs(delta) >= 0.0001f && !UseTween)
    {
	    tween.TweenCallback(Callable.From(() =>
      {
		parentGridObject.visualMesh.Quaternion = targetRotation;
      }));
    }
    else if (Mathf.Abs(delta) >= 0.0001f)
    {
      float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);

	  var tw = tween.TweenMethod(
		  Callable.From<float>(weight =>
			  parentGridObject.visualMesh.Quaternion = startRotation
				  .Slerp(targetRotation, weight)
				  .Normalized()),
		  0.0f,
		  1.0f,
		  duration);
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
	  _lastCommittedRotation = parentGridObject.visualMesh.Quaternion;
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

			  if (!parentGridObject.GridPositionData.TrySetDirection(nextDirection))
			  {
				  parentGridObject.visualMesh.Quaternion = _lastCommittedRotation;
				  _rotationBlocked = true;
				  return;
			  }
			  currentDirection = nextDirection;
			  currentYaw = parentGridObject.visualMesh.Rotation.Y;
			  _lastCommittedRotation = parentGridObject.visualMesh.Quaternion;
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
	  if (FirstArrival.Scripts.Managers.ActionManager.Instance != null)
		  await FirstArrival.Scripts.Managers.ActionManager.Instance.YieldIfActionBudgetExceeded();
	  if (IsCancellationRequested) return false;
	  float targetYawRad = RotationHelperFunctions.GetRotationRadians(direction);
	  float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
	  float finalYaw = currentYaw + delta;
	  float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);
	  Quaternion targetRotation = new Quaternion(Vector3.Up, finalYaw).Normalized();

	  if (!ShouldAnimate() || duration < 0.0001f)
	  {
		  parentGridObject.visualMesh.Quaternion = targetRotation;
		  return !IsCancellationRequested;
	  }

	  Quaternion startRotation = parentGridObject.visualMesh.Quaternion.Normalized();
	  _rotationTween = ApplyAnimationSpeed(parentGridObject.visualMesh.CreateTween());
	  _rotationTween.SetTrans(Tween.TransitionType.Sine);
	  _rotationTween.SetEase(Tween.EaseType.InOut);
	  _rotationTween.TweenMethod(
		  Callable.From<float>(weight =>
			  parentGridObject.visualMesh.Quaternion = startRotation
				  .Slerp(targetRotation, weight)
				  .Normalized()),
		  0.0f,
		  1.0f,
		  duration);

	  bool completed = await WaitForTween(_rotationTween);
	  _rotationTween = null;
	  if (completed)
		  parentGridObject.visualMesh.Quaternion = targetRotation;
	  return completed;
  }

  protected override Task ActionComplete()
  {
	  if (_rotationBlocked)
		  return CancelCall();

	  // The action's target is the source of truth. Deriving this from a
	  // transform reintroduces rounding/model-forward-offset errors.
	  return parentGridObject.GridPositionData.TrySetDirection(_targetDirection)
		  ? Task.CompletedTask
		  : CancelCall();
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
		  // Keep any fully completed intermediate steps and discard only the
		  // partially animated step that was interrupted.
		  parentGridObject.visualMesh.Quaternion = _lastCommittedRotation;
	  }

	  return Task.CompletedTask;
  }
}
