using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class Pathfinder : Manager<Pathfinder>
{
	/// <summary>
	/// Returns a path (list of GridCells) from start to goal using A*.
	/// If adjacentIsValid is true, finds a path to the closest cell adjacent to the goal.
	/// Returns an empty list if no path is found.
	/// </summary>
	public List<GridCell> FindPath(GridCell start, GridCell goal, bool adjacentIsValid = false)
	{
		return FindPathInternal(start, goal, adjacentIsValid);
	}

	/// <summary>
	/// Asynchronous version of FindPath that can be awaited
	/// </summary>
	public async Task<List<GridCell>> FindPathAsync(GridCell start, GridCell goal, bool adjacentIsValid = false,
		CancellationToken cancellationToken = default)
	{
		return await Task.Run(() => FindPathInternal(start, goal, adjacentIsValid), cancellationToken);
	}

	/// <summary>
	/// Internal implementation of the pathfinding algorithm
	/// </summary>
	private List<GridCell> FindPathInternal(
		GridCell start,
		GridCell goal,
		bool adjacentIsValid = false
	)
	{
		// Quick check for invalid inputs
		if (start == null || goal == null) return new List<GridCell>();

		// If adjacentIsValid is false, the goal must be walkable
		if (!adjacentIsValid && !goal.state.HasFlag(Enums.GridCellState.Walkable))
			return new List<GridCell>();

		// If start and goal are the same cell and goal is walkable, return just that
		if (start == goal) return new List<GridCell> { start };

		// For adjacentIsValid, collect all walkable cells adjacent to the goal
		HashSet<GridCell> validTargets = new HashSet<GridCell>();
		if (adjacentIsValid)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				foreach (var offset in new Vector3I[]
				         {
					         new Vector3I(1, dy, 0),
					         new Vector3I(-1, dy, 0),
					         new Vector3I(0, dy, 1),
					         new Vector3I(0, dy, -1),
					         new Vector3I(1, dy, 1),
					         new Vector3I(1, dy, -1),
					         new Vector3I(-1, dy, 1),
					         new Vector3I(-1, dy, -1),
				         })
				{
					Vector3I neighborCoords = new Vector3I(
						goal.gridCoordinates.X + offset.X,
						goal.gridCoordinates.Y + offset.Y,
						goal.gridCoordinates.Z + offset.Z
					);

					var gridSystem = GetGridSystemThreadSafe();
					if (gridSystem == null) continue;
					var gridCells = gridSystem.GridCells;

					if (
						neighborCoords.Y >= 0
						&& neighborCoords.Y < gridCells.Length
						&& neighborCoords.X >= 0
						&& neighborCoords.X
						< gridCells[neighborCoords.Y].GetLength(0)
						&& neighborCoords.Z >= 0
						&& neighborCoords.Z
						< gridCells[neighborCoords.Y].GetLength(1)
					)
					{
						GridCell neighbor = gridCells[neighborCoords.Y][
							neighborCoords.X,
							neighborCoords.Z
						];
						if (
							neighbor != null
							&& neighbor.state.HasFlag(
								Enums.GridCellState.Walkable
							)
						)
						{
							validTargets.Add(neighbor);
						}
					}
				}
			}

			// If there are no walkable cells adjacent to the goal, return empty path
			if (validTargets.Count == 0) return new List<GridCell>();

			// If the start cell is one of the valid targets, return just that cell
			if (validTargets.Contains(start)) return new List<GridCell> { start };
		}
		else
		{
			// If not checking for adjacency, the only valid target is the goal itself
			validTargets.Add(goal);
		}

		// Our node record used during the A* search.
		var openList = new List<NodeRecord>();
		var closedSet = new HashSet<GridCell>();

		// Create the start record.
		NodeRecord startRecord = new NodeRecord
		{
			Cell = start,
			Parent = null,
			CostSoFar = 0,
			EstimatedTotalCost = validTargets.Count == 1
				? Heuristic(start, validTargets.First())
				: validTargets.Min(t => Heuristic(start, t)),
		};
		openList.Add(startRecord);

		NodeRecord current = null;
		NodeRecord targetRecord = null;

		while (openList.Count > 0)
		{
			// Select the node with the lowest estimated total cost (f = g + h)
			current = openList.OrderBy(n => n.EstimatedTotalCost).First();

			// If we've reached any valid target, we're done.
			if (validTargets.Contains(current.Cell))
			{
				targetRecord = current;
				break;
			}

			openList.Remove(current);
			closedSet.Add(current.Cell);

			foreach (GridCell neighbor in GetNeighbors(current.Cell))
			{
				// Skip if neighbor is already evaluated.
				if (closedSet.Contains(neighbor)) continue;

				float cost = current.CostSoFar + Cost(current.Cell, neighbor);

				// Try to find an existing record for this neighbor.
				NodeRecord neighborRecord =
					openList.FirstOrDefault(n => n.Cell == neighbor);

				if (neighborRecord == null)
				{
					// Create a new record.
					float heuristic = validTargets.Count == 1
						? Heuristic(neighbor, validTargets.First())
						: validTargets.Min(t => Heuristic(neighbor, t));

					neighborRecord = new NodeRecord
					{
						Cell = neighbor,
						CostSoFar = cost,
						EstimatedTotalCost = cost + heuristic,
						Parent = current,
					};
					openList.Add(neighborRecord);
				}
				else if (cost < neighborRecord.CostSoFar)
				{
					// Update the record since we found a cheaper path.
					neighborRecord.CostSoFar = cost;

					float heuristic = validTargets.Count == 1
						? Heuristic(neighbor, validTargets.First())
						: validTargets.Min(t => Heuristic(neighbor, t));

					neighborRecord.EstimatedTotalCost = cost + heuristic;
					neighborRecord.Parent = current;
				}
			}
		}

		// If we never reached a valid target, return an empty path.
		if (targetRecord == null) return new List<GridCell>();

		// Reconstruct the path.
		var path = new List<GridCell>();
		current = targetRecord;
		while (current != null)
		{
			path.Add(current.Cell);
			current = current.Parent;
		}

		path.Reverse();

		return path;
	}

	/// <summary>
	/// Overload that accepts coordinate tuples
	/// </summary>
	public List<GridCell> FindPath(Vector3I startCoords, Vector3I goalCoords, bool adjacentIsValid = false)
	{
		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
			return new List<GridCell>();

		return FindPath(
			gridSystem.GetGridCell(startCoords),
			gridSystem.GetGridCell(goalCoords),
			adjacentIsValid
		);
	}

	/// <summary>
	/// Async overload that accepts coordinate tuples
	/// </summary>
	public async Task<List<GridCell>> FindPathAsync(
		Vector3I startCoords,
		Vector3I goalCoords,
		bool adjacentIsValid = false,
		CancellationToken cancellationToken = default)
	{
		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
			return new List<GridCell>();

		return await FindPathAsync(
			gridSystem.GetGridCell(startCoords),
			gridSystem.GetGridCell(goalCoords),
			adjacentIsValid,
			cancellationToken
		);
	}

	/// <summary>
	/// Returns true if a path exists from start to goal, or to a cell adjacent to goal if adjacentIsValid is true.
	/// </summary>
	public bool IsPathPossible(GridCell start, GridCell goal, bool adjacentIsValid = false)
	{
		return IsPathPossibleInternal(start, goal, adjacentIsValid);
	}

	/// <summary>
	/// Async version of IsPathPossible
	/// </summary>
	public async Task<bool> IsPathPossibleAsync(GridCell start, GridCell goal, bool adjacentIsValid = false,
		CancellationToken cancellationToken = default)
	{
		return await Task.Run(() => IsPathPossibleInternal(start, goal, adjacentIsValid), cancellationToken);
	}

	/// <summary>
	/// Internal implementation of IsPathPossible
	/// </summary>
	private bool IsPathPossibleInternal(GridCell start, GridCell goal, bool adjacentIsValid = false)
	{
		// Quick check for invalid inputs
		if ((start == null || start == GridCell.Null) || (goal == null || goal == GridCell.Null))
			return false;

		// If adjacentIsValid is false, the goal must be walkable
		if (!adjacentIsValid && !goal.state.HasFlag(Enums.GridCellState.Walkable))
			return false;

		// If start and goal are the same cell, return true
		if (start == goal && goal.state.HasFlag(Enums.GridCellState.Walkable))
			return true;

		// If adjacentIsValid and start is adjacent to goal, return true immediately
		if (adjacentIsValid && GetNeighbors(start).Contains(goal))
			return true;

		// For adjacentIsValid, we'll collect all cells adjacent to the goal
		HashSet<GridCell> validTargets = new HashSet<GridCell>();
		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
			return false;

		var gridCells = gridSystem.GridCells;

		if (adjacentIsValid)
		{
			for (int y = goal.gridCoordinates.Y - 1; y <= goal.gridCoordinates.Y + 1; y++)
			{
				// Get all walkable cells adjacent to the goal
				foreach (var offset in new Vector3I[]
				         {
					         new Vector3I(1, y, 0),
					         new Vector3I(-1, y, 0),
					         new Vector3I(0, y, 1),
					         new Vector3I(0, y, -1),
					         new Vector3I(1, y, 1),
					         new Vector3I(1, y, -1),
					         new Vector3I(-1, y, 1),
					         new Vector3I(-1, y, -1)
				         })
				{
					Vector3I neighborCoords = new Vector3I(
						goal.gridCoordinates.X + offset.X,
						y,
						goal.gridCoordinates.Z + offset.Z
					);

					// Check if neighborCoords are within the grid bounds
					if (y >= 0 && y < gridCells.Length &&
					    neighborCoords.X >= 0 && neighborCoords.X < gridCells[y].GetLength(0) &&
					    neighborCoords.Z >= 0 && neighborCoords.Z < gridCells[y].GetLength(1))
					{
						GridCell neighbor = gridCells[y][neighborCoords.X, neighborCoords.Z];
						if (neighbor.state.HasFlag(Enums.GridCellState.Walkable))
							validTargets.Add(neighbor);
					}
				}
			}

			// If there are no walkable cells adjacent to the goal, return false
			if (validTargets.Count == 0)
				return false;

			// If the start cell is one of the valid targets, return true
			if (validTargets.Contains(start))
				return true;
		}
		else
		{
			// If not checking for adjacency, the only valid target is the goal itself
			validTargets.Add(goal);
		}

		// Our node record used during the search
		var openSet = new HashSet<GridCell>();
		var closedSet = new HashSet<GridCell>();

		// Priority queue would be better, but using a simple list for compatibility
		var openList = new List<NodeRecord>();

		// Create the start record
		NodeRecord startRecord = new NodeRecord
		{
			Cell = start,
			Parent = null,
			CostSoFar = 0,
			EstimatedTotalCost = Heuristic(start, goal)
		};
		openList.Add(startRecord);
		openSet.Add(start);

		while (openList.Count > 0)
		{
			// Select the node with the lowest estimated total cost
			NodeRecord current = openList.OrderBy(n => n.EstimatedTotalCost).First();

			// If we've reached any of the valid targets, a path exists
			if (validTargets.Contains(current.Cell))
				return true;

			openList.Remove(current);
			openSet.Remove(current.Cell);
			closedSet.Add(current.Cell);

			foreach (GridCell neighbor in GetNeighbors(current.Cell))
			{
				// Skip if neighbor is already evaluated
				if (closedSet.Contains(neighbor))
					continue;

				float cost = current.CostSoFar + Cost(current.Cell, neighbor);

				// If this node is not in the open set, add it
				if (!openSet.Contains(neighbor))
				{
					NodeRecord neighborRecord = new NodeRecord
					{
						Cell = neighbor,
						CostSoFar = cost,
						EstimatedTotalCost = cost + Heuristic(neighbor, goal),
						Parent = current
					};
					openList.Add(neighborRecord);
					openSet.Add(neighbor);
				}
				else
				{
					// Find the existing record
					NodeRecord existingRecord = openList.First(n => n.Cell == neighbor);

					// If we found a better path, update it
					if (cost < existingRecord.CostSoFar)
					{
						existingRecord.CostSoFar = cost;
						existingRecord.EstimatedTotalCost = cost + Heuristic(neighbor, goal);
						existingRecord.Parent = current;
					}
				}
			}
		}

		// If we've exhausted all possibilities without finding a valid target, no path exists
		return false;
	}

	/// <summary>
	/// Overload that accepts coordinate tuples
	/// </summary>
	public bool IsPathPossible(Vector3I startCoords, Vector3I goalCoords, bool adjacentIsValid = false)
	{
		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
			return false;

		return IsPathPossible(
			gridSystem.GetGridCell(startCoords),
			gridSystem.GetGridCell(goalCoords),
			adjacentIsValid
		);
	}

	/// <summary>
	/// Async overload that accepts coordinate tuples
	/// </summary>
	public async Task<bool> IsPathPossibleAsync(
		Vector3I startCoords,
		Vector3I goalCoords,
		bool adjacentIsValid = false,
		CancellationToken cancellationToken = default)
	{
		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
			return false;

		return await IsPathPossibleAsync(
			gridSystem.GetGridCell(startCoords),
			gridSystem.GetGridCell(goalCoords),
			adjacentIsValid,
			cancellationToken
		);
	}

	/// <summary>
	/// Returns the total cost of a given path.
	/// </summary>
	public float GetPathCost(List<GridCell> path)
	{
		if (path == null || path.Count == 0)
			return float.PositiveInfinity;

		float totalCost = 0f;
		for (int i = 1; i < path.Count; i++)
			totalCost += Cost(path[i - 1], path[i]);
		return totalCost;
	}

	private float Heuristic(GridCell a, GridCell b)
	{
		int dx = Mathf.Abs(a.gridCoordinates.X - b.gridCoordinates.X);
		int dy = Mathf.Abs(a.gridCoordinates.Y - b.gridCoordinates.Y);
		int dz = Mathf.Abs(a.gridCoordinates.Z - b.gridCoordinates.Z);

		float D = 1f; // axis
		float D2 = 1.4142136f; // sqrt(2), 2D diagonal
		float D3 = 1.7320508f; // sqrt(3), 3D diagonal

		int aMax = Mathf.Max(dx, Mathf.Max(dy, dz));
		int cMin = Mathf.Min(dx, Mathf.Min(dy, dz));
		int bMid = dx + dy + dz - aMax - cMin;

		// Equivalent to counting the number of 3D, then 2D, then axis steps
		return D3 * cMin + D2 * (bMid - cMin) + D * (aMax - bMid);
	}

	/// <summary>
	/// Returns the cost to move from cell a to cell b.
	/// For uniform grids, this is typically 1.
	/// </summary>
	private float Cost(GridCell a, GridCell b)
	{
		int dx = Mathf.Abs(a.gridCoordinates.X - b.gridCoordinates.X);
		int dy = Mathf.Abs(a.gridCoordinates.Y - b.gridCoordinates.Y);
		int dz = Mathf.Abs(a.gridCoordinates.Z - b.gridCoordinates.Z);

		int sum = dx + dy + dz;
		if (sum == 1) return 1f; // axis-aligned
		if (sum == 2) return 1.4142136f; // 2D diagonal
		if (sum == 3) return 1.7320508f; // 3D diagonal

		// Shouldn't happen with our neighbor generator, fall back:
		return 1f + 0.0001f * sum;
	}

	/// <summary>
	/// Returns a list of walkable neighbor cells for a given cell.
	/// Checks 6 cardinal directions in a 3D grid.
	/// </summary>
	private List<GridCell> GetNeighbors(GridCell cell)
	{
		if (!GridSystem.Instance.TryGetGridCellNeighbors(cell, out var neighbors))
			return new List<GridCell>();

		return neighbors;
	}

	#region Arc Pathfinding

public class ArcPathResult
{
    public bool Success { get; set; }
    public List<GridCell> GridCellPath { get; set; } = new List<GridCell>();
    public Godot.Collections.Array<Vector3> Vector3Path { get; set; } = new Godot.Collections.Array<Vector3>();
}

public ArcPathResult TryCalculateArcPath(GridCell startCell, GridCell endCell, int attempts = 3)
{
    var result = new ArcPathResult { Success = false };
    
    if (startCell == null || endCell == null)
    {
        GD.Print("Start or end cell is null");
        return result;
    }

    // Check if start or end is obstructed
    if ((startCell.state & Enums.GridCellState.Obstructed) != 0 ||
        (endCell.state & Enums.GridCellState.Obstructed) != 0)
    {
        GD.Print("Start or end point is obstructed.");
        return result;
    }

    Vector3 startPos = startCell.worldCenter;
    Vector3 endPos = endCell.worldCenter;
    Vector3 direction = endPos - startPos;
    float distance = direction.Length();

    // Too close - return direct path
    if (distance < 0.1f)
    {
        result.Success = true;
        result.GridCellPath.Add(startCell);
        result.Vector3Path.Add(startPos);
        return result;
    }

    GridSystem gridSystem = GridSystem.Instance;
    Vector2 cellSize = MeshTerrainGenerator.Instance.cellSize; // Assuming you have this property

    // Try different arc heights
    for (int attempt = 0; attempt < attempts; attempt++)
    {
        // Calculate arc height with variation for each attempt
        float heightFactor = 0.3f + (attempt * 0.2f); // Start lower, go higher
        float arcHeight = distance * heightFactor;
        

        // Adaptive number of points based on distance and cell size
        int numPoints = Mathf.Max(10, (int)(distance / Mathf.Min(cellSize.X, cellSize.X) * 2));

        // Reset result for this attempt
        var attemptResult = new ArcPathResult { Success = false };
        GridCell lastGridCell = null;
        var smoothPath = new Godot.Collections.Array<Vector3>();

        bool pathValid = true;

        for (int i = 0; i <= numPoints; i++)
        {
            float t = (float)i / numPoints; // Interpolation factor (0 to 1)

            // Calculate the current position along the straight line
            Vector3 currentPos = startPos.Lerp(endPos, t);

            // Calculate the vertical offset for the arc (parabolic shape)
            float verticalOffset = -4 * arcHeight * t * (t - 1); // Parabola formula

            // Apply the vertical offset to create the arc
            Vector3 arcPos = currentPos + Vector3.Up * verticalOffset;

            // Store the smooth path position
            smoothPath.Add(arcPos);

            // Convert world position to grid coordinates
            var getGridCellResult = gridSystem.TyGetGridCellFromWorldPosition(arcPos, out GridCell cell, true);
            
            if (!getGridCellResult)
            {
                GD.Print($"Failed to get grid cell at position: {arcPos}");
                pathValid = false;
                break;
            }

            GridCell gridCell = cell;

            // For arc paths, allow AIR spaces but block solid obstacles
            // Adjust this condition based on your Enums.CellState values
            if ((gridCell.state & Enums.GridCellState.Obstructed) != 0 &&
                (gridCell.state & Enums.GridCellState.Obstructed) == 0)
            {
                GD.Print($"Obstacle detected at: {gridCell.gridCoordinates}");
                pathValid = false;
                break;
            }

            // Only add the grid cell if it's different from the last one
            if (lastGridCell == null || !AreGridCellsEqual(lastGridCell, gridCell))
            {
                attemptResult.GridCellPath.Add(gridCell);
                lastGridCell = gridCell;
            }
        }

        // If path is valid, return it with both paths
        if (pathValid && attemptResult.GridCellPath.Count > 0)
        {
            attemptResult.Success = true;
            attemptResult.Vector3Path = smoothPath;
            return attemptResult;
        }
    }

    // If all attempts failed
    GD.Print($"All {attempts} attempts failed to find a valid arc path");
    return result;
}

// Helper function to compare grid cells
private bool AreGridCellsEqual(GridCell cell1, GridCell cell2)
{
    if (cell1 == null || cell2 == null)
        return cell1 == cell2;
    
    return cell1.gridCoordinates == cell2.gridCoordinates;
}

	#endregion

	/// <summary>
	/// Helper class for A* search.
	/// </summary>
	private class NodeRecord
	{
		public GridCell Cell;
		public NodeRecord Parent;
		public float CostSoFar; // g value
		public float EstimatedTotalCost; // f = g + h
	}


	/// <summary>
	/// Thread-safe access to GridSystem.Instance
	/// </summary>
	private GridSystem GetGridSystemThreadSafe()
	{
		// This method ensures we access GridSystem.Instance in a thread-safe way
		// For a more robust solution, you might want to cache the reference or use a dependency injection pattern
		return GridSystem.Instance;
	}

	protected override Task _Setup()
	{
		return Task.CompletedTask;
	}

	protected override Task _Execute()
	{
		return Task.CompletedTask;
	}
}