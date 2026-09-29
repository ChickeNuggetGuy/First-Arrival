using Godot;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MeleeAttackActionDefinition
	: ItemActionDefinition
{
	protected override bool TargetsGridObjects => true;

	[Export] public int damage;
	[Export] public bool canCauseFatalWounds = true;
	[Export] public bool dealsStunDamage = false;

	[ExportGroup("Action Cost")] [Export(PropertyHint.Range, "1,100,1")]
	public int timeUnitCost = 24;

	[Export(PropertyHint.Range, "0,100,1")]
	public int staminaCost = 16;

	public override ActionBase InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	)
	{
		return new MeleeAttackActionBase(parent, startGridCell, targetGridCell, this, costs)
		{
			Item = Item
		};
	}

	protected override bool OnValidateAndBuildCosts(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs,
		out string reason
	)
	{
		if (Item == null)
		{
			reason = "No melee item equipped";
			return false;
		}


		if (!gridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions))
		{
			reason = "No grid object Action found";
			return false;
		}

		if (!targetGridCell.HasGridObject())
		{
			reason = "No grid object found";
			return false;
		}

		GridObject targetGridObject = GetTarget(gridObject, targetGridCell);

		if (targetGridObject == null)
		{
			reason = "Target grid object is null, failed all conditions";
			return false;
		}


		var targetCells = new List<GridCell> { targetGridCell };
		GridCell actionAnchor = gridObject.GridPositionData.AnchorCell
		                        ?? startingGridCell;
		bool canAttackFromCurrentAnchor = GridFootprintUtility
			.TryGetAdjacentFacingTarget(
				gridObject,
				actionAnchor,
				targetCells,
				out GridCell facingTarget,
				out _
			);

		if (canAttackFromCurrentAnchor)
		{
			if (!AddRotateCostsIfNeeded(
				    gridObject,
				    actionAnchor,
				    facingTarget,
				    costs,
				    out string rotateReason,
				    actionAnchor))
			{
				reason = rotateReason;
				return false;
			}
		}
		else
		{
			var moveAction = gridObjectActions.ActionDefinitions
					.FirstOrDefault(action => action is MoveActionDefinition)
				as MoveActionDefinition;
			if (moveAction == null)
			{
				reason = "Unit cannot move when movement is required";
				return false;
			}

			bool foundDestination = false;
			string lastMoveReason = "No adjacent anchor fits the unit footprint";
			foreach (GridCell candidate in GridFootprintUtility
				         .GetAdjacentAnchorCandidates(gridObject, targetCells))
			{
				if (!moveAction.TryBuildCostsOnly(
					    gridObject,
					    startingGridCell,
					    candidate,
					    out var moveCosts,
					    out lastMoveReason))
					continue;

				if (!GridFootprintUtility.TryGetAdjacentFacingTarget(
					    gridObject,
					    candidate,
					    targetCells,
					    out GridCell candidateFacingTarget,
					    out _))
					continue;

				var combinedCosts = new Godot.Collections.Dictionary<Enums.Stat, int>();
				AddCosts(combinedCosts, moveCosts);
				Enums.Direction arrivalDirection = moveAction.path?.Count >= 2
					? RotationHelperFunctions.GetDirectionBetweenCells(
						moveAction.path[^2],
						moveAction.path[^1]
					)
					: gridObject.GridPositionData.Direction;
				if (!AddRotateCostsIfNeeded(
					    gridObject,
					    candidate,
					    candidateFacingTarget,
					    combinedCosts,
					    out _,
					    candidate,
					    arrivalDirection))
					continue;

				AddCosts(costs, combinedCosts);
				foundDestination = true;
				break;
			}

			if (!foundDestination)
			{
				reason = $"Cannot reach melee range: {lastMoveReason}";
				return false;
			}
		}

		AddCost(costs, Enums.Stat.TimeUnits, timeUnitCost);
		AddCost(costs, Enums.Stat.Stamina, staminaCost);

		reason = "success";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		return parentGridObject.TeamHolder.GetVisibleGridCells().Where(cell =>
		{
			if (!cell.HasGridObject()) return false;
			return true;
		}).ToList();
	}

	// Grid cells also contain terrain metadata. Validation, setup and execution
	// must resolve the same damageable occupant rather than the first object.
	public GridObject GetTarget(GridObject actor, GridCell cell)
	{
		if (actor == null || cell?.gridObjects == null) return null;
		return cell.gridObjects.Where(candidate =>
				candidate != null && candidate is not GridCellStateOverride &&
				candidate.IsActive && candidate != actor && candidate.Team != actor.Team &&
				candidate.TryGetGridObjectNode<GridObjectStatHolder>(out var stats) &&
				stats.TryGetStat(Enums.Stat.Health, out var health) && health.CurrentValue > 0 &&
				(!dealsStunDamage || candidate.Condition?.Stun != null))
			.OrderBy(candidate => candidate.scenery)
			.FirstOrDefault();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(GridCell targetGridCell)
	{
		return (targetGridCell, GetTarget(parentGridObject, targetGridCell) == null ? 0 : 100);
	}


	public override bool GetIsUIAction() => true;
	public override string GetActionName() => dealsStunDamage ? "Stun" : "Melee";
	public override MouseButton GetActionInput() => MouseButton.Left;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => true;
}