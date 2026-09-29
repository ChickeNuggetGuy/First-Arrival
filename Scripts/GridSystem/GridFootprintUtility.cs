using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

/// <summary>
/// Shared spatial queries for units whose gameplay position occupies more than
/// one grid cell. Actions should compare footprints through this class instead
/// of assuming the anchor is the unit's only cell.
/// </summary>
public static class GridFootprintUtility
{
	public static List<GridCell> GetOccupiedCells(
		GridObject gridObject,
		GridCell fallbackCell = null
	)
	{
		if (gridObject?.GridPositionData?.OccupiedCells is { Count: > 0 } occupied)
			return occupied.Where(cell => cell != null).Distinct().ToList();

		GridCell anchor = gridObject?.GridPositionData?.AnchorCell ?? fallbackCell;
		return anchor == null ? new List<GridCell>() : new List<GridCell> { anchor };
	}

	public static bool AreAdjacent(
		IEnumerable<GridCell> firstCells,
		IEnumerable<GridCell> secondCells
	)
	{
		if (firstCells == null || secondCells == null)
			return false;

		foreach (GridCell first in firstCells)
		{
			if (first == null) continue;
			foreach (GridCell second in secondCells)
			{
				if (second == null) continue;
				Vector3I delta = second.GridCoordinates - first.GridCoordinates;
				if (delta == Vector3I.Zero) continue;

				if (Mathf.Abs(delta.X) <= 1 &&
				    Mathf.Abs(delta.Y) <= 1 &&
				    Mathf.Abs(delta.Z) <= 1)
					return true;
			}
		}

		return false;
	}

	public static bool TryGetClosestPair(
		IEnumerable<GridCell> firstCells,
		IEnumerable<GridCell> secondCells,
		out GridCell firstResult,
		out GridCell secondResult
	)
	{
		firstResult = null;
		secondResult = null;
		long closestDistance = long.MaxValue;

		if (firstCells == null || secondCells == null)
			return false;

		foreach (GridCell first in firstCells)
		{
			if (first == null) continue;
			foreach (GridCell second in secondCells)
			{
				if (second == null) continue;
				long distance = first.GridCoordinates.DistanceSquaredTo(
					second.GridCoordinates
				);
				if (distance >= closestDistance) continue;

				closestDistance = distance;
				firstResult = first;
				secondResult = second;
			}
		}

		return firstResult != null && secondResult != null;
	}

	public static float GetClosestGridDistance(
		IEnumerable<GridCell> firstCells,
		IEnumerable<GridCell> secondCells
	)
	{
		return TryGetClosestPair(
			firstCells,
			secondCells,
			out GridCell first,
			out GridCell second
		)
			? first.GridCoordinates.DistanceTo(second.GridCoordinates)
			: float.PositiveInfinity;
	}

	public static bool TryGetAdjacentFacingTarget(
		GridObject mover,
		GridCell anchor,
		IEnumerable<GridCell> targetCells,
		out GridCell facingTarget,
		out Enums.Direction direction
	)
	{
		facingTarget = null;
		direction = Enums.Direction.None;
		GridPositionData position = mover?.GridPositionData;
		if (position == null || anchor == null || targetCells == null)
			return false;

		foreach (GridCell target in targetCells
		         .Where(cell => cell != null)
		         .OrderBy(cell => anchor.GridCoordinates.DistanceSquaredTo(
			         cell.GridCoordinates)))
		{
			Enums.Direction candidateDirection = RotationHelperFunctions
				.GetDirectionBetweenCells(anchor, target);
			if (candidateDirection == Enums.Direction.None)
				candidateDirection = position.Direction;

			if (!position.CanOccupyAt(anchor, candidateDirection))
				continue;

			if (!AreAdjacent(
				    position.GetGridCellsAt(anchor, candidateDirection),
				    targetCells))
				continue;

			facingTarget = target;
			direction = candidateDirection;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Finds anchor cells where the mover's complete, target-facing footprint is
	/// valid and at least one occupied cell is adjacent to a target cell.
	/// </summary>
	public static List<GridCell> GetAdjacentAnchorCandidates(
		GridObject mover,
		IEnumerable<GridCell> targetCells
	)
	{
		var results = new List<GridCell>();
		GridPositionData position = mover?.GridPositionData;
		List<GridCell> targets = targetCells?
			.Where(cell => cell != null)
			.Distinct()
			.ToList() ?? new List<GridCell>();
		if (position?.Shape == null || targets.Count == 0)
			return results;

		foreach (GridCell anchor in GetNearbyAnchorCandidates(mover, targets))
		{
			if (TryGetAdjacentFacingTarget(
				    mover,
				    anchor,
				    targets,
				    out _,
				    out _))
				results.Add(anchor);
		}

		return results;
	}

	/// <summary>
	/// Returns possible anchor cells in the bounded area around a target. No
	/// facing or occupancy assumption is made, so callers can validate the
	/// arrival direction produced by their own path.
	/// </summary>
	public static List<GridCell> GetNearbyAnchorCandidates(
		GridObject mover,
		IEnumerable<GridCell> targetCells
	)
	{
		GridPositionData position = mover?.GridPositionData;
		GridSystem gridSystem = GridSystem.Instance;
		List<GridCell> targets = targetCells?
			.Where(cell => cell != null)
			.Distinct()
			.ToList() ?? new List<GridCell>();
		if (position?.Shape == null || gridSystem == null || targets.Count == 0)
			return new List<GridCell>();

		int horizontalRadius = Mathf.Max(position.Shape.SizeX, position.Shape.SizeZ) + 1;
		int verticalRadius = position.Shape.SizeY + 1;
		var candidateCoordinates = new HashSet<Vector3I>();
		foreach (GridCell target in targets)
		{
			for (int y = -verticalRadius; y <= verticalRadius; y++)
			{
				for (int x = -horizontalRadius; x <= horizontalRadius; x++)
				{
					for (int z = -horizontalRadius; z <= horizontalRadius; z++)
					{
						candidateCoordinates.Add(
							target.GridCoordinates + new Vector3I(x, y, z)
						);
					}
				}
			}
		}

		GridCell currentAnchor = position.AnchorCell;
		return candidateCoordinates
			.Select(coordinate => gridSystem.GetGridCell(coordinate))
			.Where(cell => cell != null)
			.Distinct()
			.OrderBy(cell => currentAnchor == null
				? 0
				: currentAnchor.GridCoordinates.DistanceSquaredTo(cell.GridCoordinates))
			.ToList();
	}
}
