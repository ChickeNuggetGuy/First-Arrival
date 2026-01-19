using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public class MoveStepAction : Action, ICompositeAction
{
  public Action ParentAction { get; set; }
  public List<Action> SubActions { get; set; }
  public Enums.Direction targetDirection { get; set; }


  public Vector2 blendSpaceValue;

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
	    blendSpaceValue.X = -1;
    }
    blendSpaceValue.Y = 0;

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
	
   
    parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Moving);
    parentGridObject.animationNode.TrySetParameter("moveBlendSpace", blendSpaceValue);
    
    Tween tween = parentGridObject.CreateTween();
    tween.SetTrans(Tween.TransitionType.Linear);
    tween.SetEase(Tween.EaseType.OutIn);
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
    if (NextAction == null || NextAction is not MoveStepAction)
    {
	    parentGridObject.animationNode.SetLocomotionType(Enums.LocomotionType.Idle);
    }
    else if (NextAction is MoveStepAction moveAction)
    {
	    parentGridObject.animationNode.TrySetParameter("moveBlendSpace", moveAction.blendSpaceValue);
    }
    parentGridObject.animationNode.TrySetParameter("moveBlendSpace", Vector2.Zero);
    return Task.CompletedTask;
  }
}