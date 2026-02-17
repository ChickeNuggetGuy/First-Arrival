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
		if (start == null || goal == null)
		{
			GD.Print("Either start or goal is null.");
			return new List<GridCell>();
		}

		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
		{
			GD.Print("GridSystem is null.");
			return new List<GridCell>();
		}

		// If adjacentIsValid is false, the goal must be walkable
		if (!adjacentIsValid && !goal.IsWalkable)
		{
			GD.Print("Goal is not walkable (no connections).");
			return new List<GridCell>();
		}

		// If start and goal are the same cell, return just that
		if (start.GridCoordinates == goal.GridCoordinates)
		{
			GD.Print("Start and goal are equal.");
			return new List<GridCell> { start };
		}
		
		HashSet<GridCell> validTargets = new HashSet<GridCell>();
		if (adjacentIsValid)
		{
			// Get all neighbors of the goal cell is adjacent is valid
			var goalNeighbors = GetNeighborsInRadius(gridSystem, goal.GridCoordinates, 1);
			
			foreach (var neighbor in goalNeighbors)
			{
				// Only include walkable neighbors
				if (neighbor.IsWalkable)
				{
					validTargets.Add(neighbor);
				}
			}
			
			if (validTargets.Count == 0)
			{
				GD.Print("No valid walkable targets adjacent to goal.");
				return new List<GridCell>();
			}

			// If the start cell is one of the valid targets, return just that
			if (validTargets.Contains(start))
			{
				GD.Print("Start is adjacent to goal.");
				return new List<GridCell> { start };
			}
		}
		else
		{
			// If not checking for adjacency, the only valid target is the goal itself
			validTargets.Add(goal);
		}

		// node record
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
			// f = g + h
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
				if (closedSet.Contains(neighbor))
					continue;

				float cost = current.CostSoFar + Cost(current.Cell, neighbor);


				NodeRecord neighborRecord =
					openList.FirstOrDefault(n => n.Cell == neighbor);

				if (neighborRecord == null)
				{
					// Create a new record.
					float heuristic =
						validTargets.Count == 1
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

					float heuristic =
						validTargets.Count == 1
							? Heuristic(neighbor, validTargets.First())
							: validTargets.Min(t => Heuristic(neighbor, t));

					neighborRecord.EstimatedTotalCost = cost + heuristic;
					neighborRecord.Parent = current;
				}
			}
		}

		// If we never reached a valid target, return an empty path.
		if (targetRecord == null)
		{
			GD.Print("Target record is null - no path found.");
			return new List<GridCell>();
		}

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

	private bool IsPathPossibleInternal(
		GridCell start,
		GridCell goal,
		bool adjacentIsValid = false
	)
	{
		if ((start == null || start == GridCell.Null) || (goal == null || goal == GridCell.Null))
			return false;

		var gridSystem = GetGridSystemThreadSafe();
		if (gridSystem == null)
			return false;

		// If adjacentIsValid is false, the goal must be walkable
		if (!adjacentIsValid && !goal.IsWalkable)
			return false;

		// If start and goal are the same cell, require it to be walkable
		if (start == goal)
			return start.IsWalkable;
		
		HashSet<GridCell> validTargets = new HashSet<GridCell>();

		if (adjacentIsValid)
		{
			var goalNeighbors = GetNeighborsInRadius(gridSystem, goal.GridCoordinates, 1);
			
			foreach (var neighbor in goalNeighbors)
			{
				if (neighbor.IsWalkable)
				{
					validTargets.Add(neighbor);
				}
			}

			// If there are no adjacent cells with connections, return false
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

		//  node record 
		var openSet = new HashSet<GridCell>();
		var closedSet = new HashSet<GridCell>();
		var openList = new List<NodeRecord>();
		
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
					NodeRecord existingRecord =
						openList.First(n => n.Cell == neighbor);

					// If we found a better path, update it
					if (cost < existingRecord.CostSoFar)
					{
						existingRecord.CostSoFar = cost;
						existingRecord.EstimatedTotalCost =
							cost + Heuristic(neighbor, goal);
						existingRecord.Parent = current;
					}
				}
			}
		}

		// If no valid target, no path exists
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
		int dx = Mathf.Abs(a.GridCoordinates.X - b.GridCoordinates.X);
		int dy = Mathf.Abs(a.GridCoordinates.Y - b.GridCoordinates.Y);
		int dz = Mathf.Abs(a.GridCoordinates.Z - b.GridCoordinates.Z);

		float D = 1f; 
		// sqrt(2), 2D diagonal
		float D2 = 1.4142136f; 
		// sqrt(3), 3D diagonal
		float D3 = 1.7320508f; 

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
		int dx = Mathf.Abs(a.GridCoordinates.X - b.GridCoordinates.X);
		int dy = Mathf.Abs(a.GridCoordinates.Y - b.GridCoordinates.Y);
		int dz = Mathf.Abs(a.GridCoordinates.Z - b.GridCoordinates.Z);

		int sum = dx + dy + dz;
		// axis-aligned
		if (sum == 1) return 1f;
		// 2D diagonal
		if (sum == 2) return 1.4142136f; 
		// 3D diagonal
		if (sum == 3) return 1.7320508f; 

		// Shouldn't happen with our neighbor generator, fall back
		return 1f + 0.0001f * sum;
	}

	/// <summary>
	/// Returns a list of walkable neighbor cells for a given cell.
	/// Uses the centralized connection system via GridSystem.
	/// </summary>
	private List<GridCell> GetNeighbors(GridCell cell)
	{
		if (!GridSystem.Instance.TryGetGridCellNeighbors(cell, true, false, out var neighbors))
			return new List<GridCell>();

		return neighbors;
	}

	/// <summary>
	/// Helper method to get all neighbors within a given radius (in grid cells).
	/// Used for finding adjacent cells for adjacentIsValid parameter.
	/// </summary>
	private List<GridCell> GetNeighborsInRadius(GridSystem gridSystem, Vector3I center, int radius)
	{
		var neighbors = new List<GridCell>();

		for (int dy = -radius; dy <= radius; dy++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dz = -radius; dz <= radius; dz++)
				{
					if (dx == 0 && dy == 0 && dz == 0)
						continue;

					Vector3I neighborCoords = center + new Vector3I(dx, dy, dz);
					GridCell neighbor = gridSystem.GetGridCell(neighborCoords);
					
					if (neighbor != null)
					{
						neighbors.Add(neighbor);
					}
				}
			}
		}

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

		Vector3 startPos = startCell.WorldCenter;
		Vector3 endPos = endCell.WorldCenter;
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
		Vector2 cellSize = MeshTerrainGenerator.Instance.cellSize;

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
				var getGridCellResult = gridSystem.TryGetGridCellFromWorldPosition(arcPos, out GridCell cell, true);
				
				if (!getGridCellResult)
				{
					GD.Print($"Failed to get grid cell at position: {arcPos}");
					pathValid = false;
					break;
				}

				GridCell gridCell = cell;

				// For arc paths, allow AIR spaces but block solid obstacles
				if ((gridCell.state & Enums.GridCellState.Obstructed) != 0)
				{
					GD.Print($"Obstacle detected at: {gridCell.GridCoordinates}");
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
		
		return cell1.GridCoordinates == cell2.GridCoordinates;
	}

	#endregion

	/// <summary>
	/// Helper class for A* search.
	/// </summary>
	private class NodeRecord
	{
		public GridCell Cell;
		public NodeRecord Parent;
		// g value
		public float CostSoFar; 
		// f = g + h
		public float EstimatedTotalCost;
	}

	/// <summary>
	/// Thread-safe access to GridSystem.Instance
	/// </summary>
	private GridSystem GetGridSystemThreadSafe()
	{
		return GridSystem.Instance;
	}

	public override string GetManagerName() =>"Pathfinder";

	protected override Task _Setup(bool loadingData)
	{
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		return Task.CompletedTask;
	}
	
	#region manager Data
	public override void Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		base.Load(data);
		if(!HasLoadedData) return;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		return null;
	}
	#endregion
	
	public override void Deinitialize()
	{
		return;
	}
}