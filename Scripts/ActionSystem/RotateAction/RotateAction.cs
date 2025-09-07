using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

public partial class RotateAction : Action
{
  private Enums.Direction _targetDirection;

  // 8-way yaw angles in degrees. Adjust these values if North/South are backwards.
  // Default assumes: North=0°, East=90°, South=180°, West=270°
  private static readonly Dictionary<Enums.Direction, float> DirectionYawDegrees = new()
  {
    { Enums.Direction.North, 0f },
    { Enums.Direction.NorthEast, 45f },
    { Enums.Direction.East, 90f },
    { Enums.Direction.SouthEast, 135f },
    { Enums.Direction.South, 180f },
    { Enums.Direction.SouthWest, 225f },
    { Enums.Direction.West, 270f },
    { Enums.Direction.NorthWest, 315f }
  };

  // Tween settings
  private const float TurnSpeedDegPerSec = 540f;
  private const bool UseTween = true;


  public RotateAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, (Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data, Enums.Direction targetDirection) : base(parentGridObject, startingGridCell, targetGridCell, data)
  {
	  _targetDirection = targetDirection;
  }

  protected override async Task Setup()
  {
    _targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(
      startingGridCell,
      targetGridCell
    );
    await Task.CompletedTask;
  }

  protected override async Task Execute()
  {
    if (_targetDirection == Enums.Direction.None)
    {
      await Task.CompletedTask;
      return;
    }

    // Get the target yaw angle for this direction
    if (!DirectionYawDegrees.TryGetValue(_targetDirection, out float targetYawDeg))
    {
      await Task.CompletedTask;
      return;
    }

    float targetYawRad = Mathf.DegToRad(targetYawDeg);

    if (!UseTween)
    {
      var r = parentGridObject.Rotation;
      r.Y = targetYawRad;
      parentGridObject.Rotation = r;
      await Task.CompletedTask;
      return;
    }

    // Tween to target using shortest path
    float currentYaw = parentGridObject.Rotation.Y;
    float delta = Mathf.Wrap(targetYawRad - currentYaw, -Mathf.Pi, Mathf.Pi);
    float finalYaw = currentYaw + delta;

    float duration = Mathf.Abs(delta) / Mathf.DegToRad(TurnSpeedDegPerSec);
    if (duration < 0.0001f)
    {
      var r = parentGridObject.Rotation;
      r.Y = targetYawRad;
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
	  parentGridObject.GridPositionData.SetDirection(_targetDirection);
    await Task.CompletedTask;
  }
}