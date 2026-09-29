using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class MeleeAttackActionBase : ActionBase, ICompositeAction, IItemAction
{
	public Item Item { get; set; }
	public ActionBase ParentActionBase { get; set; }
	public List<ActionBase> SubActions { get; set; }

	public List<GridCell> path = new List<GridCell>();

	public MeleeAttackActionBase(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,
		ActionDefinition parentAction,
		Godot.Collections.Dictionary<Enums.Stat, int> costs) :
		base(parentGridObject, startingGridCell, targetGridCell, parentAction,
		costs)
	{
	}

	protected override async Task Setup()
	{
		ParentActionBase = this;


			if (!parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectNodes)) return;
            var definition = parentActionDefinition as MeleeAttackActionDefinition;
            GridObject targetObject = definition?.GetTarget(parentGridObject, targetGridCell);
			if (targetObject == null) return;

			var targetCells = new List<GridCell> { targetGridCell };
			GridCell actionAnchor = parentGridObject.GridPositionData.AnchorCell
			                        ?? startingGridCell;
			if (GridFootprintUtility.TryGetAdjacentFacingTarget(
				    parentGridObject,
				    actionAnchor,
				    targetCells,
				    out GridCell facingTarget,
				    out _))
			{
				AddRotateSubActionIfNeeded(actionAnchor, facingTarget);
				return;
			}

			MoveActionDefinition moveActionDefinition =
				gridObjectNodes.ActionDefinitions.FirstOrDefault(a => a is MoveActionDefinition) as MoveActionDefinition;

		if (moveActionDefinition == null)
		{
			GD.Print("HELP: Move action definition not found");
			return;
		}

			foreach (GridCell moveDestination in GridFootprintUtility
			         .GetAdjacentAnchorCandidates(parentGridObject, targetCells))
			{
				if (!moveActionDefinition.TryBuildCostsOnly(
					    parentGridObject,
					    startingGridCell,
					    moveDestination,
					    out var executionMoveCosts,
					    out _))
					continue;

				if (!GridFootprintUtility.TryGetAdjacentFacingTarget(
					    parentGridObject,
					    moveDestination,
					    targetCells,
					    out GridCell destinationFacingTarget,
					    out _))
					continue;

				MoveActionBase moveActionBase = moveActionDefinition.InstantiateAction(
					parentGridObject,
					startingGridCell,
					moveDestination,
					new Godot.Collections.Dictionary<Enums.Stat, int>()
				) as MoveActionBase;
				AddSubAction(moveActionBase);
				foreach (var moveCost in executionMoveCosts)
				{
					if (costs.ContainsKey(moveCost.Key))
						costs[moveCost.Key] -= moveCost.Value;
				}
				AddRotateSubActionIfNeeded(
					moveDestination,
					destinationFacingTarget,
					force: true
				);
				return;
			}

			GD.PrintErr("MeleeAttackAction.Setup: No reachable anchor fits the unit footprint.");
	}

	protected override async Task Execute()
	{
		GD.Print("Melee Attck Execute");
        var meleeAttackActionDefinition = parentActionDefinition as MeleeAttackActionDefinition;
        GridObject targetGridObject = meleeAttackActionDefinition?.GetTarget(parentGridObject, targetGridCell);

		if (targetGridObject == null)
		{
			GD.Print("Target grid object is null, failed all conditions");
			return;
		}

		if (!targetGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return;


		if (!statHolder.TryGetStat(Enums.Stat.Health, out var health))
		{
			GD.Print("Target Grid Object does not have Health stat");
			return;
		}



		var damage = meleeAttackActionDefinition.damage;
		if (meleeAttackActionDefinition.dealsStunDamage)
		{
            var condition = targetGridObject.Condition;
            float previousStun = condition.Stun.CurrentValue;
            condition.ApplyStun(damage);
            GD.Print($"{targetGridObject.Name}: +{condition.Stun.CurrentValue - previousStun:0.#} stun " +
                $"({condition.Stun.CurrentValue:0.#}/{health.CurrentValue:0.#} to knock out; {condition.State}).");
            return;
		}

		GridObjectStat.DamageResult damageResult =
			health.ApplyDamage(damage, meleeAttackActionDefinition.canCauseFatalWounds);
		GD.Print(
			$"Target unit damaged for {damageResult.HealthDamage} damage, " +
			$"remaining health is {health.CurrentValue}, fatal wounds added: " +
			$"{damageResult.FatalWoundsAdded} ({damageResult.WoundedBodyPart})"
		);

		return;
	}

	protected override async Task ActionComplete()
	{
		return;
	}
}
