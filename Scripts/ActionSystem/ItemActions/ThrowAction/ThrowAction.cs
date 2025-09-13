using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.ItemActions.ThrowAction;

public class ThrowAction : Action, ICompositeAction, IItemAction
{
  // ICompositeAction bridge to base Parent
  public Action ParentAction
  {
    get => Parent;
    set => SetParent(value);
  }

  public List<Action> SubActions { get; set; } = new();

  // IItemAction
  public Item Item { get; set; }

  protected GridCell[] path;
  protected Vector3[] vectorPath;

  public ThrowAction() { }

  public ThrowAction(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parentAction,
    Item item,
    List<GridCell> p,
    Vector3[] vPath,
    Dictionary<Enums.Stat, int> costs
  ) : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
  {
    Item = item;
    path = p?.ToArray();
    vectorPath = vPath;

    if (vectorPath == null || vectorPath.Length == 0)
      GD.Print("Path not found!");
  }

  protected override Task Setup()
  {
    if (path == null || path.Length == 0)
    {
      GD.Print("Setup Path not found!");
      return Task.CompletedTask;
    }

    var currentDirection = parentGridObject.GridPositionData.Direction;
    var targetDirection =
      RotationHelperFunctions.GetDirectionBetweenCells(
        startingGridCell,
        targetGridCell
      );

    if (currentDirection != targetDirection)
    {
      var rotateActionDefinition = parentGridObject.ActionDefinitions
        .FirstOrDefault(node => node is RotateActionDefinition)
        as RotateActionDefinition;

      if (rotateActionDefinition != null)
      {
        // Important: sub-action must not deduct costs if the parent already
        // charges aggregate costs — pass zero costs.
        var zeroCosts = new Dictionary<Enums.Stat, int>();
        var rotateAction =
          (RotateAction)rotateActionDefinition.InstantiateAction(
            parentGridObject,
            startingGridCell,
            targetGridCell,
            zeroCosts
          );

        AddSubAction(rotateAction);
      }
    }

    return Task.CompletedTask;
  }

  protected override async Task Execute()
  {
    GD.Print("ThrowAction");

    var visual = new CsgSphere3D { Radius = 0.5f };
    parentGridObject.GetTree().Root.AddChild(visual);
    visual.GlobalPosition = vectorPath[0];

    foreach (Vector3 position in vectorPath)
    {
      Tween tween = parentGridObject.CreateTween();
      tween.SetTrans(Tween.TransitionType.Linear);
      tween.SetEase(Tween.EaseType.InOut);
      tween.TweenProperty(visual, "position", position, 0.05);
      await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
    }

    if (Item == null)
    {
      GD.Print("item is null");
    }

    if (Item?.currentGrid == null)
    {
      GD.Print("Item inventory not found");
    }
    else
    {
      GD.Print("Item inventory found!");
    }

    if (!InventoryGrid.TryTransferItem(
          Item.currentGrid,
          targetGridCell.InventoryGrid,
          Item
        ))
    {
      GD.Print("Failed to transfer item");
    }
    else
    {
      GD.Print(
        $"Successfully transferred item to {targetGridCell.gridCoordinates} " +
        $"inventory {targetGridCell.InventoryGrid.ItemCount}"
      );
    }

    visual.QueueFree();
  }

  protected override Task ActionComplete()
  {
    return Task.CompletedTask;
  }
}