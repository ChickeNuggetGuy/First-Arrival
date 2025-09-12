using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.ActionSystem.ItemActions.ThrowAction;


public class ThrowAction : Action, ICompositeAction, IItemAction
{
	public Action ParentAction
	{
		get => this; set{} }
	public List<Action> SubActions { get; set; }
	public Item Item { get; set; }

	protected GridCell[] path;
	protected Vector3[] vectorPath ;
	public ThrowAction()
	{
		
	}
	public ThrowAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, ActionDefinition parentAction,
		Vector3[] vPath, System.Collections.Generic.Dictionary<Enums.Stat, int> costs) :
		base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
	{
		GridSystem gridSystem = GridSystem.Instance;
		if (parentAction is ThrowActionDefinition throwActionDefinition)
		{
			
		}
		if (vPath == null  || vPath.Length == 0)
		{
			GD.Print("Path not found!");
		}
		vectorPath = vPath;
	}

	protected override Task Setup()
	{
		ParentAction = this;
		if (path == null || path.Length == 0)
		{
			GD.Print("Setup Path not found!");
			
			return Task.CompletedTask;
		}

		GD.Print("VAR");
		
		
		Enums.Direction currentDirection = parentGridObject.GridPositionData.Direction;
		Enums.Direction targetDirection =  RotationHelperFunctions.GetDirectionBetweenCells(startingGridCell, targetGridCell);

		if (currentDirection != targetDirection)
		{
			//Not facing the corret direction, rotate first
			RotateActionDefinition rotateActionDefinition = parentGridObject.ActionDefinitions.FirstOrDefault(node => 
				node is RotateActionDefinition) as RotateActionDefinition;
			
			if (rotateActionDefinition == null) return Task.CompletedTask;

			RotateAction rotateAction = (RotateAction)rotateActionDefinition.InstantiateAction(parentGridObject, 
				startingGridCell, targetGridCell,costs);
			
			SubActions.Add(rotateAction);
		}
		return Task.CompletedTask;
	}

	protected override async Task Execute()
	{
		GD.Print("ThrowAction");
		GridSystem gridSystem = GridSystem.Instance;
		CsgSphere3D visual = new CsgSphere3D();
		visual.Radius = 0.5f;
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
		if (Item.currentGrid == null)
		{
			GD.Print("Item inventory not found");
		}
		else
		{
			GD.Print("Item inventory found!");
		}
		if (!InventoryGrid.TryTransferItem(Item.currentGrid, targetGridCell.InventoryGrid, Item))
		{
			GD.Print("Failed to transfer item");
		}
		else
		{
			GD.Print($"Successfully transfed item to {targetGridCell.gridCoordinates} inventory {targetGridCell.InventoryGrid.ItemCount}");
		}
		GD.Print("Execute Throw Action");
		visual.QueueFree();
		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}