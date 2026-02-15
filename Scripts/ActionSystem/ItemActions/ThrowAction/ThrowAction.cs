using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;


public class ThrowAction : Action, ICompositeAction, IItemAction
{
  public Action ParentAction
  {
    get => Parent;
    set => SetParent(value);
  }

  public List<Action> SubActions { get; set; } = new();
  
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
    
    if (!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActionsNode)) return Task.CompletedTask;
    if (currentDirection != targetDirection)
    {
      var rotateActionDefinition = gridObjectActionsNode.ActionDefinitions
        .FirstOrDefault(node => node is RotateActionDefinition)
        as RotateActionDefinition;

      if (rotateActionDefinition != null)
      {
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
          Item,1
        ))
    {
      GD.Print("Failed to transfer item");
    }
    else
    {
      GD.Print(
        $"Successfully transferred item to {targetGridCell.GridCoordinates} " +
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