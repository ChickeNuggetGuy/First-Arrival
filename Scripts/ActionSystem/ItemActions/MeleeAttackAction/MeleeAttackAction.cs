using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class MeleeAttackAction : Action, ICompositeAction, IItemAction
{
	
	public Item Item { get; set; }
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }
	
	public List<GridCell> path = new List<GridCell>();
	public MeleeAttackAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,ActionDefinition parentAction,
		Dictionary<Enums.Stat, int> costs) : base(parentGridObject, startingGridCell, targetGridCell ,parentAction, costs)
	{
	}
	
	protected override async Task Setup()
	{
		ParentAction = this;

		MoveActionDefinition moveActionDefinition =
			parentGridObject.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition) as MoveActionDefinition;
		
		if (moveActionDefinition == null)
		{
			GD.Print("HELP: Move action definition not found");

			return;
		}

			MoveAction moveAction = moveActionDefinition.InstantiateAction(parentGridObject,
				startingGridCell, targetGridCell, costs) as MoveAction;
			SubActions.Add(moveAction);

			return;

	}

	protected override async Task Execute()
	{
		GD.Print("Melee Attck Execute");
		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}
