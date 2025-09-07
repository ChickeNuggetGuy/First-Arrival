using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.ActionSystem.ItemActions.ThrowAction;

public class ThrowAction : ItemAction, ICompositeAction
{
	public Action ParentAction
	{
		get => this; set{} }
	public List<Action> SubActions { get; set; }
	public Item Item { get; set; }

	protected List<GridCell> path =  new List<GridCell>();
	protected Array<Vector3> vectorPath =  new Array<Vector3>();
	public ThrowAction()
	{
		
	}
	public ThrowAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,
		(System.Collections.Generic.Dictionary<Enums.Stat, int> costs, System.Collections.Generic.Dictionary<string, Variant> extraData) data) : base(parentGridObject, startingGridCell, targetGridCell, data)
	{
		GridSystem gridSystem = GridSystem.Instance;

		if (data.extraData.TryGetValue("path", out Variant pathVariant))
		{
			Array<Vector3I> p = pathVariant.As<Array<Vector3I>>();
			foreach (var gridCoords in p)
			{
				GridCell cell = gridSystem.GetGridCell(gridCoords);
				if (cell != null)
					path.Add(cell);
			}
		}
		else
		{
			GD.Print("Path not found!");
		}

		if (data.extraData.TryGetValue("vectorPath", out Variant vectorPathVariant))
		{
			vectorPath = vectorPathVariant.As<Array<Vector3>>();
		}
	}

	protected override Task Setup()
	{
		ParentAction = this;
		if (path == null || path.Count == 0)
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
				startingGridCell, targetGridCell,(costs,null));
			
			SubActions.Add(rotateAction);
		}
		return Task.CompletedTask;
	}

	protected override async Task Execute()
	{
		GD.Print("ThrowAction");
		GridSystem gridSystem = GridSystem.Instance;
		CsgSphere3D visual = new CsgSphere3D();
		visual.Radius = 2;
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

		GD.Print("Execute Throw Action");
		visual.QueueFree();
	
		targetGridCell.InventoryGrid.TryAddItem(Item);
		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}