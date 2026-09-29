using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public class MoveStepActionBase : ActionBase, ICompositeAction
{
  private bool _playedMovementVisuals;
  private Tween _movementTween;

  public ActionBase ParentActionBase { get; set; }
  public List<ActionBase> SubActions { get; set; }
  public Enums.Direction targetDirection { get; set; }

  public Vector3 TargetWorldPos => targetGridCell.WorldCenter;
  public GridCell TargetCell => targetGridCell;


  public Vector2 blendSpaceValue;

  public MoveStepActionBase(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parent,
    Godot.Collections.Dictionary<Enums.Stat, int> costs
  )
    : base(parentGridObject, startingGridCell, targetGridCell, parent, costs)
  {
  }

  protected override async Task Setup()
  {
    ParentActionBase = this;

    // Direction is the authoritative gameplay state; rotations are applied to
    // visualMesh, not necessarily to the GridObject transform.
    Enums.Direction currentDirection = parentGridObject.GridPositionData.Direction;
    targetDirection =
      RotationHelperFunctions.GetDirectionBetweenCells(
        startingGridCell,
        targetGridCell
      );

    if (!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions)) return;

    if (currentDirection != targetDirection)
    {
      // Not facing the correct direction, rotate first
      var rotateActionDefinition =
	      gridObjectActions.ActionDefinitions.FirstOrDefault(
          node => node is RotateActionDefinition
        ) as RotateActionDefinition;

      if (rotateActionDefinition == null)
      {
        await Task.CompletedTask;
        return;
      }
      
      Godot.Collections.Dictionary<Enums.Stat, int> rotateCosts = new();
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
        rotateCosts = new Godot.Collections.Dictionary<Enums.Stat, int>
        {
          { Enums.Stat.TimeUnits, Mathf.Abs(steps) * 1 },
          { Enums.Stat.Stamina, Mathf.Abs(steps) * 1 },
        };
      }

      var rotateAction =
        (RotateActionBase)rotateActionDefinition.InstantiateAction(
          parentGridObject,
          startingGridCell,
          targetGridCell,
          rotateCosts
        );

      AddSubAction(rotateAction);
      
      foreach (var kv in rotateCosts)
      {
	      if (costs.ContainsKey(kv.Key))
		      costs[kv.Key] -= kv.Value;
      }
    }
    
    // Every step first turns the unit toward its destination, so its movement
    // is always forward in model space. Selecting a strafe clip from the world
    // direction made diagonal steps slide sideways while the root moved toward
    // the correct cell.
    blendSpaceValue = Vector2.Zero;

    await Task.CompletedTask;
  }

  protected override async Task Execute()
  {
	  _playedMovementVisuals = ShouldAnimate();
	  if (!_playedMovementVisuals)
	  {
		  parentGridObject.animationNode?.SetLocomotionType(Enums.LocomotionType.Idle);
		  parentGridObject.animationNode?.TrySetParameter(
			  "WalkBlendSpace/blend_position",
			  Vector2.Zero
		  );
		  parentGridObject.GlobalPosition = targetGridCell.WorldCenter;
		  return;
	  }

	  parentGridObject.animationNode?.SetLocomotionType(Enums.LocomotionType.Moving);
	  parentGridObject.animationNode?.TrySetParameter("WalkBlendSpace/blend_position", blendSpaceValue);

	  _movementTween = ApplyAnimationSpeed(parentGridObject.CreateTween());
	  var moveTw = _movementTween.TweenProperty(
		  parentGridObject,
		  "global_position",
		  targetGridCell.WorldCenter,
		  0.5f // StepMoveDurationSec
	  );
	  moveTw.SetTrans(Tween.TransitionType.Linear);

	  bool completed = await WaitForTween(_movementTween);
	  _movementTween = null;
	  if (completed)
		  parentGridObject.GlobalPosition = targetGridCell.WorldCenter;
  }

  protected override Task ActionComplete()
  {
	  if (!parentGridObject.GridPositionData.TrySetGridCell(targetGridCell))
	  {
		  parentGridObject.GlobalPosition = startingGridCell.WorldCenter;
		  return CancelCall();
	  }
	  if (!_playedMovementVisuals)
		  return Task.CompletedTask;

	  if (NextActionBase is MoveStepActionBase nextStep)
	  {
		  parentGridObject.animationNode?.SetLocomotionType(Enums.LocomotionType.Moving);
		  parentGridObject.animationNode?.TrySetParameter(
			  "WalkBlendSpace/blend_position",
			  nextStep.blendSpaceValue
		  );
	  }
	  else
	  {
		  parentGridObject.animationNode?.SetLocomotionType(Enums.LocomotionType.Idle);
		  parentGridObject.animationNode?.TrySetParameter(
			  "WalkBlendSpace/blend_position",
			  Vector2.Zero
		  );
	  }

	  return Task.CompletedTask;
  }

  protected override Task ActionCanceled()
  {
	  if (_movementTween != null && GodotObject.IsInstanceValid(_movementTween))
		  _movementTween.Kill();
	  _movementTween = null;

	  if (parentGridObject == null || !GodotObject.IsInstanceValid(parentGridObject))
		  return Task.CompletedTask;

	  // A partial step is not committed. Return the unit to the cell from which
	  // the step began so world position and grid occupancy remain consistent.
	  if (startingGridCell != null)
	  {
		  parentGridObject.GlobalPosition = startingGridCell.WorldCenter;
		  parentGridObject.GridPositionData.SetGridCell(startingGridCell);
	  }

	  parentGridObject.animationNode?.SetLocomotionType(Enums.LocomotionType.Idle);
	  parentGridObject.animationNode?.TrySetParameter(
		  "WalkBlendSpace/blend_position",
		  Vector2.Zero
	  );

	  return Task.CompletedTask;
  }
}
