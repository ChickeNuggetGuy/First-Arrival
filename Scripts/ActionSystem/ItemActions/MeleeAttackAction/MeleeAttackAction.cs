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

		if (!GridSystem.Instance.TryGetGridCellNeighbors(targetGridCell, true, false, out var neighbors))
		{
			GD.PrintErr("MeleeAttackAction.Setup: Could not find neighbors for target gridcell");
			return;
		}

		// Are we already adjacent?
		bool isAdjacent = neighbors.Any(c => c.gridCoordinates == startingGridCell.gridCoordinates);

		if (isAdjacent)
		{
			// No move needed.
			return;
		}
		
		// Not adjacent. We need to move.
		var walkableNeighbors = neighbors.Where(n => n.IsWalkable).ToList();
		if (!walkableNeighbors.Any())
		{
			GD.PrintErr("MeleeAttackAction.Setup: No walkable cell near target to move to.");
			return;
		}

		var moveDestination = walkableNeighbors.OrderBy(n => startingGridCell.gridCoordinates.DistanceSquaredTo(n.gridCoordinates)).First();

		MoveActionDefinition moveActionDefinition =
			parentGridObject.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition) as MoveActionDefinition;
		
		if (moveActionDefinition == null)
		{
			GD.Print("HELP: Move action definition not found");
			return;
		}
		
		MoveAction moveAction = moveActionDefinition.InstantiateAction(parentGridObject,
			startingGridCell, moveDestination, new Dictionary<Enums.Stat, int>()) as MoveAction;
		AddSubAction(moveAction);
	}

	protected override async Task Execute()
	{
		GD.Print("Melee Attck Execute");
		GridObject targetGridObject = targetGridCell.gridObjects.FirstOrDefault(gridObject =>
		{
			if(gridObject == null) return false;
			if(!gridObject.IsActive) return false;
			if(gridObject == parentGridObject) return false;
			if(gridObject.Team == parentGridObject.Team) return false;
			return true;
		});

		if (targetGridObject == null)
		{
			GD.Print("Target grid object is null, failed all conditions");
			return;
		}

		if (!targetGridObject.TryGetStat(Enums.Stat.Health, out var health))
		{
			GD.Print("Target Grid Object does not have Health stat");
			return;
		}

		if (Item.ItemData.ItemSettings.HasFlag(Enums.ItemSettings.CanMelee))
		{
			health.RemoveValue(Item.ItemData.Damage);
			GD.Print($"Target unit Damaged for {Item.ItemData.Damage} damage, remaining health is {health.CurrentValue}");

		}
		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}


}
