using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

public enum ModifyStatTargetRequirement
{
	None,
	Adjacency,
	LineOfSight
}

[GlobalClass]
public partial class ModifyStatActionDefinition : ActionDefinition
{
	[ExportGroup("Stat Modifiers")]
	[Export]
	public Godot.Collections.Dictionary<Enums.Stat, int> targetStats = new();

	[Export]
	public ModifyStatTargetRequirement targetRequirement =
		ModifyStatTargetRequirement.Adjacency;

	[ExportGroup("Action Cost")]
	[Export(PropertyHint.Range, "0,100,1")]
	public int timeUnitCost;

	[Export(PropertyHint.Range, "0,100,1")]
	public int staminaCost;

	[ExportGroup("UI")]
	[Export]
	public string actionName = "Modify Stat";

	public override ActionBase InstantiateAction(
		GridObject parent,
		GridCell startGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	)
	{
		return new ModifyStatAction(
			parent,
			startGridCell,
			targetGridCell,
			this,
			costs
		);
	}

	protected override bool OnValidateAndBuildCosts(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		Godot.Collections.Dictionary<Enums.Stat, int> costs,
		out string reason
	)
	{
		if (!HasStatModifiers())
		{
			reason = "No non-zero stat modifiers are configured";
			return false;
		}

		if (!TryGetTargetGridObject(gridObject, targetGridCell, out _))
		{
			reason = "No active grid object in the target cell has a configured stat";
			return false;
		}

		if (targetRequirement == ModifyStatTargetRequirement.Adjacency)
		{
			if (!IsWithinAdjacencyRange(startingGridCell, targetGridCell))
			{
				if (!TryBuildMoveToTargetAdjacency(
					gridObject,
					startingGridCell,
					targetGridCell,
					out _,
					out _,
					out var moveCosts,
					out reason
				))
					return false;

				AddCosts(costs, moveCosts);
			}
		}
		else if (!MeetsTargetRequirement(
			gridObject,
			startingGridCell,
			targetGridCell,
			out reason
		))
		{
			return false;
		}

		AddCost(costs, Enums.Stat.TimeUnits, timeUnitCost);
		AddCost(costs, Enums.Stat.Stamina, staminaCost);

		reason = "Success!";
		return true;
	}

	protected override List<GridCell> GetValidGridCells(
		GridObject gridObject,
		GridCell startingGridCell
	)
	{
		if (
			gridObject == null
			|| startingGridCell == null
			|| !HasStatModifiers()
			|| GridSystem.Instance == null
		)
			return new List<GridCell>();

		IEnumerable<GridCell> candidateCells;
		switch (targetRequirement)
		{
			case ModifyStatTargetRequirement.Adjacency:
				candidateCells = GridSystem.Instance.AllGridCells;
				break;

			case ModifyStatTargetRequirement.LineOfSight:
				if (!TryGetCurrentSight(gridObject, out GridObjectSight sight))
					return new List<GridCell>();

				candidateCells = sight.VisibleCells;
				break;

			default:
				candidateCells = GridSystem.Instance.AllGridCells;
				break;
		}

		return candidateCells
			.Where(cell =>
				cell != null
				&& cell != GridCell.Null
				&& TryGetTargetGridObject(gridObject, cell, out _)
			)
			.Distinct()
			.Where(cell => CanTakeAction(
				gridObject,
				startingGridCell,
				cell,
				out _,
				out _
			))
			.ToList();
	}

	public override (GridCell gridCell, int score) GetAIActionScore(
		GridCell targetGridCell
	)
	{
		if (!TryGetTargetGridObject(parentGridObject, targetGridCell, out var target))
			return (targetGridCell, 0);

		if (!target.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder))
			return (targetGridCell, 0);

		bool sameTeam = parentGridObject == target
			|| (parentGridObject != null
				&& (parentGridObject.Team & target.Team) != Enums.UnitTeam.None);
		float utility = 0;

		foreach (var modifier in targetStats)
		{
			if (
				modifier.Key == Enums.Stat.None
				|| modifier.Value == 0
				|| !statHolder.TryGetStat(modifier.Key, out GridObjectStat stat)
			)
				continue;

			float changedValue = Mathf.Clamp(
				stat.CurrentValue + modifier.Value,
				stat.MinMaxValue.min,
				stat.MinMaxValue.max
			) - stat.CurrentValue;

			// Increasing a stat is useful on an ally, while decreasing one is
			// useful on a non-ally. Clamping naturally scores full/empty stats at 0.
			utility += sameTeam ? changedValue : -changedValue;
		}

		return (targetGridCell, Mathf.RoundToInt(utility));
	}

	internal bool TryGetTargetGridObject(
		GridObject actingGridObject,
		GridCell targetGridCell,
		out GridObject targetGridObject
	)
	{
		targetGridObject = null;
		if (targetGridCell?.gridObjects == null || !HasStatModifiers())
			return false;

		GridObjectSight sight = null;
		if (
			targetRequirement == ModifyStatTargetRequirement.LineOfSight
			&& !TryGetCurrentSight(actingGridObject, out sight)
		)
			return false;

		foreach (GridObject candidate in targetGridCell.gridObjects)
		{
			if (candidate == null || !candidate.IsActive)
				continue;

			if (sight != null && !sight.SeenGridObjects.Contains(candidate))
				continue;

			if (!candidate.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder))
				continue;

			if (!targetStats.Any(modifier =>
				modifier.Key != Enums.Stat.None
				&& modifier.Value != 0
				&& statHolder.TryGetStat(modifier.Key, out _)
			))
				continue;

			targetGridObject = candidate;
			return true;
		}

		return false;
	}

	private bool HasStatModifiers()
	{
		return targetStats != null && targetStats.Any(modifier =>
			modifier.Key != Enums.Stat.None && modifier.Value != 0
		);
	}

	internal bool IsWithinAdjacencyRange(
		GridCell startingGridCell,
		GridCell targetGridCell
	)
	{
		if (
			startingGridCell == null
			|| targetGridCell == null
			|| GridSystem.Instance == null
		)
			return false;

		// Same-cell targeting permits a unit to use an adjacent-range support
		// action on itself without stepping away first.
		if (startingGridCell.GridCoordinates == targetGridCell.GridCoordinates)
			return true;

		return GridSystem.Instance.TryGetGridCellNeighbors(
			targetGridCell,
			false,
			false,
			out List<GridCell> neighbors
		) && neighbors.Any(cell =>
			cell.GridCoordinates == startingGridCell.GridCoordinates
		);
	}

	internal bool TryBuildMoveToTargetAdjacency(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		out MoveActionDefinition moveDefinition,
		out GridCell moveDestination,
		out Godot.Collections.Dictionary<Enums.Stat, int> moveCosts,
		out string reason
	)
	{
		moveDefinition = null;
		moveDestination = null;
		moveCosts = null;

		if (GridSystem.Instance == null || Pathfinder.Instance == null)
		{
			reason = "Grid or pathfinding system is unavailable";
			return false;
		}

		if (!gridObject.TryGetGridObjectNode<GridObjectActions>(out var actions))
		{
			reason = "Acting grid object has no actions component";
			return false;
		}

		moveDefinition = actions.ActionDefinitions?
			.OfType<MoveActionDefinition>()
			.FirstOrDefault();
		if (moveDefinition == null)
		{
			reason = "Acting grid object has no move action";
			return false;
		}

		if (!GridSystem.Instance.TryGetGridCellNeighbors(
			targetGridCell,
			false,
			false,
			out List<GridCell> neighbors
		))
		{
			reason = "Target has no neighboring grid cells";
			return false;
		}

		int bestCombinedCost = int.MaxValue;
		int bestTimeUnitCost = int.MaxValue;
		foreach (GridCell candidate in neighbors)
		{
			if (
				candidate == null
				|| !candidate.IsWalkable
				|| candidate.HasMovementBlockingGridObject()
			)
				continue;

			if (!moveDefinition.TryBuildCostsOnly(
				gridObject,
				startingGridCell,
				candidate,
				out var candidateCosts,
				out _
			))
				continue;

			int candidateTimeUnits = candidateCosts.GetValueOrDefault(
				Enums.Stat.TimeUnits,
				0
			);
			int combinedCost = candidateTimeUnits + candidateCosts.GetValueOrDefault(
				Enums.Stat.Stamina,
				0
			);

			if (
				combinedCost > bestCombinedCost
				|| (combinedCost == bestCombinedCost
					&& candidateTimeUnits >= bestTimeUnitCost)
			)
				continue;

			bestCombinedCost = combinedCost;
			bestTimeUnitCost = candidateTimeUnits;
			moveDestination = candidate;
		}

		if (moveDestination == null)
		{
			reason = "No reachable, unoccupied cell is adjacent to the target";
			return false;
		}

		// Rebuild the selected plan last so MoveActionDefinition.path matches the
		// destination that the action will instantiate during setup.
		if (!moveDefinition.TryBuildCostsOnly(
			gridObject,
			startingGridCell,
			moveDestination,
			out moveCosts,
			out reason
		))
			return false;

		reason = "Success!";
		return true;
	}

	private bool MeetsTargetRequirement(
		GridObject gridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		out string reason
	)
	{
		switch (targetRequirement)
		{
			case ModifyStatTargetRequirement.LineOfSight:
				if (!TryGetCurrentSight(gridObject, out GridObjectSight sight))
				{
					reason = "Acting grid object has no line-of-sight component";
					return false;
				}

				if (!sight.VisibleCells.Contains(targetGridCell))
				{
					reason = "Target is not in line of sight";
					return false;
				}
				break;
		}

		reason = string.Empty;
		return true;
	}

	private static bool TryGetCurrentSight(
		GridObject gridObject,
		out GridObjectSight sight
	)
	{
		sight = null;
		if (
			gridObject == null
			|| !gridObject.TryGetGridObjectNode(out sight)
			|| sight == null
		)
			return false;

		sight.EnsureUpToDate();
		return true;
	}

	public override bool GetIsUIAction() => true;
	public override string GetActionName() => actionName;
	public override MouseButton GetActionInput() => MouseButton.Left;
	public override bool GetIsAlwaysActive() => false;
	public override bool GetRemainSelected() => false;
}
