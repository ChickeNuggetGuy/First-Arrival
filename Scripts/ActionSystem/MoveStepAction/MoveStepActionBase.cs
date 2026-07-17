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
    
    //Setup animation
    Enums.Stance stance = parentGridObject.CurrentStance;
    blendSpaceValue = new Vector2(0,0);
    if (targetDirection == Enums.Direction.North || targetDirection == Enums.Direction.South
                                                 || targetDirection == Enums.Direction.East ||
                                                 targetDirection == Enums.Direction.West)
    {
	    //Moving 
	    blendSpaceValue.X = 0;
    }
    else if (targetDirection == Enums.Direction.NorthEast || targetDirection == Enums.Direction.SouthEast)
    {
	    blendSpaceValue.X = 1;
    }
    else if (targetDirection == Enums.Direction.NorthWest || targetDirection == Enums.Direction.SouthWest)
    {
	    blendSpaceValue.X = 0;
    }
    blendSpaceValue.Y = 0;

    await Task.CompletedTask;
  }

  protected override async Task Execute()
  {
	  _playedMovementVisuals = ShouldAnimate();
	  if (!_playedMovementVisuals)
	  {
		  parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Idle);
		  parentGridObject.animationNode.TrySetParameter(
			  "WalkBlendSpace/blend_position",
			  Vector2.Zero
		  );
		  parentGridObject.Position = targetGridCell.WorldCenter;
		  return;
	  }

	  parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Moving);
	  parentGridObject.animationNode.TrySetParameter("WalkBlendSpace/blend_position", blendSpaceValue);

	  _movementTween = ApplyAnimationSpeed(parentGridObject.CreateTween());
	  var moveTw = _movementTween.TweenProperty(
		  parentGridObject,
		  "position",
		  targetGridCell.WorldCenter,
		  0.5f // StepMoveDurationSec
	  );
	  moveTw.SetTrans(Tween.TransitionType.Linear);

	  await WaitForTween(_movementTween);
	  _movementTween = null;
  }

  protected override Task ActionComplete()
  {
	  parentGridObject.GridPositionData.SetGridCell(targetGridCell);
	  if (!_playedMovementVisuals)
		  return Task.CompletedTask;

	  if (NextActionBase is MoveStepActionBase nextStep)
	  {
		  parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Moving);
		  parentGridObject.animationNode.TrySetParameter(
			  "WalkBlendSpace/blend_position",
			  nextStep.blendSpaceValue
		  );
	  }
	  else
	  {
		  parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Idle);
		  parentGridObject.animationNode.TrySetParameter(
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
		  parentGridObject.Position = startingGridCell.WorldCenter;
		  parentGridObject.GridPositionData.SetGridCell(startingGridCell);
	  }

	  parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Idle);
	  parentGridObject.animationNode.TrySetParameter(
		  "WalkBlendSpace/blend_position",
		  Vector2.Zero
	  );

	  return Task.CompletedTask;
  }
}
