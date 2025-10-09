using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DoorGridObject : GridObject, IInteractableGridobject
{
	[Export] private Node3D pivot;
	[Export] public Godot.Collections.Dictionary<Enums.Stat, int> costs { get; set; }
	
	[ExportGroup("Door Configuration")]
	[Export] public Vector3I doorSize ;
	[Export] public Vector3I doorOffset = Vector3I.Zero;
	
	public bool isOpen { get; protected set; } = false;
	
	private bool _initialized = false;
	private List<GridCell> _doorCells = new List<GridCell>();
	private HashSet<Vector3I> _doorCoords = new HashSet<Vector3I>();

	public override async Task Initialize(Enums.UnitTeam team, GridCell g)
	{ 
		await base.Initialize(team, g);
		
		if (_initialized)
		{
			GD.PrintErr("Door already initialized!");
			return;
		}

		GridSystem gridSystem = GridSystem.Instance;
		if (gridSystem == null)
		{
			GD.PrintErr("GridSystem is null during door initialization!");
			return;
		}

		GridCell rootGridCell = GridPositionData.GridCell;
		if (rootGridCell == null)
		{
			GD.PrintErr("Root grid cell is null during door initialization!");
			return;
		}

		GD.Print($"\n=== Initializing Door at {rootGridCell.gridCoordinates} ===");
		GD.Print($"Door Size: {doorSize}");
		GD.Print($"Door Offset: {doorOffset}");

		_doorCells.Clear();
		_doorCoords.Clear();

		// Calculate door cells based on size and offset
		for (int y = 0; y < doorSize.Y; y++) 
		{
			for (int x = 0; x < doorSize.X; x++)
			{
				for (int z = 0; z < doorSize.Z; z++)
				{
					Vector3I cellCoords = new Vector3I(
						rootGridCell.gridCoordinates.X + doorOffset.X + x,
						rootGridCell.gridCoordinates.Y + doorOffset.Y + y,
						rootGridCell.gridCoordinates.Z + doorOffset.Z + z
					);

					GridCell gridCell = gridSystem.GetGridCell(cellCoords);
					
					if (gridCell == null)
					{
						GD.PrintErr($"Door cell at {cellCoords} is null!");
						continue;
					}

					_doorCells.Add(gridCell);
					_doorCoords.Add(cellCoords);

					GD.Print($"Added door cell: {cellCoords}");
				}
			}
		}

		if (_doorCells.Count == 0)
		{
			GD.PrintErr("No door cells were added during initialization!");
			return;
		}

		GD.Print($"Initialized {_doorCells.Count} door cells");

		// Set initial state - door starts closed
		foreach (var doorCell in _doorCells)
		{
			// Mark as obstructed and optionally add a custom flag if needed
			Enums.GridCellState newState = doorCell.state | Enums.GridCellState.Obstructed;
			// doorCell.SetState(newState);
			// doorCell.ModifyOriginalState(newState);
		}

		// Close connections for the initially closed door
		CloseDoorConnections(gridSystem, _doorCells);

		_initialized = true;
		GD.Print("=== Door Initialization Complete ===\n");
	}

	public void Interact()
	{
		if (!_initialized)
		{
			GD.PrintErr("Door not initialized, cannot interact!");
			return;
		}

		isOpen = !isOpen;

		GridSystem gridSystem = GridSystem.Instance;
		if (gridSystem == null)
		{
			GD.PrintErr("GridSystem instance is null in DoorGridObject.Interact()");
			return;
		}

		GD.Print($"\n=== Door is now {(isOpen ? "OPENING" : "CLOSING")} ===");

		if (_doorCells.Count == 0)
		{
			GD.PrintErr("No door cells found!");
			return;
		}

		// Update visual state
		if (isOpen)
		{
			pivot?.Hide();
		}
		else
		{
			pivot?.Show();
		}

		// Update cell states
		foreach (var doorCell in _doorCells)
		{
			Enums.GridCellState currentState = doorCell.state;
			Enums.GridCellState newState;

			if (isOpen)
			{
				// Door is opening - remove obstruction
				newState = currentState & ~Enums.GridCellState.Obstructed;
				GD.Print($"Opening door cell at {doorCell.gridCoordinates}: {currentState} -> {newState}");
			}
			else
			{
				// Door is closing - add obstruction
				newState = currentState | Enums.GridCellState.Obstructed;
				GD.Print($"Closing door cell at {doorCell.gridCoordinates}: {currentState} -> {newState}");
			}

			// Update state without triggering connection updates (we'll do it manually)
			doorCell.SetState(newState);
			doorCell.ModifyOriginalState(newState);
		}

		// Now rebuild all connections for door cells and their neighbors
		if (isOpen)
		{
			OpenDoorConnections(gridSystem, _doorCells);
		}
		else
		{
			CloseDoorConnections(gridSystem, _doorCells);
		}

		GD.Print($"=== Door {(isOpen ? "OPENED" : "CLOSED")} - Connection update complete ===\n");

		// Verify connections were updated
		VerifyDoorConnections();
	}

	/// <summary>
	/// Opens the door by rebuilding connections through door cells
	/// </summary>
	private void OpenDoorConnections(GridSystem gridSystem, List<GridCell> doorCells)
	{
		GD.Print("Rebuilding connections for open door...");

		// Track all affected cells (door cells + their neighbors)
		var affectedCells = new HashSet<Vector3I>();

		foreach (var doorCell in doorCells)
		{
			affectedCells.Add(doorCell.gridCoordinates);

			// Add all neighbors (26 in 3D)
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						if (dx == 0 && dy == 0 && dz == 0) continue;

						Vector3I neighborCoords = doorCell.gridCoordinates + new Vector3I(dx, dy, dz);
						var neighbor = gridSystem.GetGridCell(neighborCoords);
						if (neighbor != null)
						{
							affectedCells.Add(neighborCoords);
						}
					}
				}
			}
		}

		// Rebuild connections for all affected cells
		foreach (var cellCoords in affectedCells)
		{
			gridSystem.UpdateGridCell(cellCoords);
		}

		GD.Print($"Rebuilt connections for {affectedCells.Count} cells");
	}

	/// <summary>
	/// Closes the door by removing connections that pass through door cells
	/// </summary>
	private void CloseDoorConnections(GridSystem gridSystem, List<GridCell> doorCells)
	{
		GD.Print("Removing connections through closed door...");

		// First, clear all connections from door cells themselves
		foreach (var doorCell in doorCells)
		{
			gridSystem.ClearConnectionsForCell(doorCell.gridCoordinates);
			GD.Print($"Cleared connections for door cell {doorCell.gridCoordinates}");
		}

		// Then rebuild connections for all neighbors
		// This will naturally exclude the obstructed door cells
		var neighborsToUpdate = new HashSet<Vector3I>();

		foreach (var doorCell in doorCells)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						if (dx == 0 && dy == 0 && dz == 0) continue;

						Vector3I neighborCoords = doorCell.gridCoordinates + new Vector3I(dx, dy, dz);
						var neighbor = gridSystem.GetGridCell(neighborCoords);
						
						// Only update non-door cells
						if (neighbor != null && !_doorCoords.Contains(neighborCoords))
						{
							neighborsToUpdate.Add(neighborCoords);
						}
					}
				}
			}
		}

		// Rebuild connections for neighbors (will exclude obstructed door)
		foreach (var neighborCoords in neighborsToUpdate)
		{
			gridSystem.UpdateGridCell(neighborCoords);
		}

		GD.Print($"Updated {neighborsToUpdate.Count} neighbor cells");
	}

	/// <summary>
	/// Check if a cell coordinate is part of this door
	/// </summary>
	public bool IsDoorCell(Vector3I coords)
	{
		return _doorCoords.Contains(coords);
	}

	/// <summary>
	/// Check if a grid cell is part of this door
	/// </summary>
	public bool IsDoorCell(GridCell cell)
	{
		return cell != null && _doorCoords.Contains(cell.gridCoordinates);
	}

	/// <summary>
	/// Debug method to verify door connections are properly updated
	/// </summary>
	private void VerifyDoorConnections()
	{
		GD.Print("\n=== DOOR CONNECTION VERIFICATION ===");
		
		GridSystem gridSystem = GridSystem.Instance;
		if (gridSystem == null) return;

		int totalDoorCellConnections = 0;

		foreach (var doorCell in _doorCells)
		{
			var connections = gridSystem.GetConnections(doorCell.gridCoordinates);
			totalDoorCellConnections += connections.Count;

			GD.Print($"Door cell at {doorCell.gridCoordinates}:");
			GD.Print($"  State: {doorCell.state}");
			GD.Print($"  IsWalkable: {doorCell.IsWalkable}");
			GD.Print($"  Connections: {connections.Count}");
			
			if (connections.Count > 0)
			{
				GD.Print($"  Connected to:");
				foreach (var connectedCoord in connections)
				{
					var connectedCell = gridSystem.GetGridCell(connectedCoord);
					string cellInfo = connectedCell != null 
						? $"{connectedCoord} (State: {connectedCell.state})" 
						: $"{connectedCoord} (NULL)";
					GD.Print($"    - {cellInfo}");
				}
			}
		}

		GD.Print($"Total door cell connections: {totalDoorCellConnections}");
		GD.Print($"Expected: {(isOpen ? "> 0" : "0")}");
		GD.Print("=== END VERIFICATION ===\n");
	}

	/// <summary>
	/// Test pathfinding through the door
	/// </summary>
	public void TestPathfindingThroughDoor()
	{
		if (!_initialized || _doorCells.Count == 0)
		{
			GD.Print("Door not initialized or no door cells");
			return;
		}

		GridSystem gridSystem = GridSystem.Instance;
		if (gridSystem == null) return;

		// Use the first door cell as reference
		GridCell doorCell = _doorCells[0];

		GD.Print("\n=== PATHFINDING TEST ===");
		GD.Print($"Using door cell: {doorCell.gridCoordinates}");

		// Try to find neighbors on opposite sides of the door
		var neighbors = new List<GridCell>();
		
		// Check all 8 cardinal/diagonal directions on the same level
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dz = -1; dz <= 1; dz++)
			{
				if (dx == 0 && dz == 0) continue;
				
				var neighborCoords = doorCell.gridCoordinates + new Vector3I(dx, 0, dz);
				var neighbor = gridSystem.GetGridCell(neighborCoords);
				
				if (neighbor != null && 
				    !IsDoorCell(neighbor) &&
				    neighbor.state.HasFlag(Enums.GridCellState.Ground))
				{
					neighbors.Add(neighbor);
				}
			}
		}

		if (neighbors.Count >= 2)
		{
			var startCell = neighbors[0];
			var endCell = neighbors[neighbors.Count - 1];

			GD.Print($"Testing path from {startCell.gridCoordinates} to {endCell.gridCoordinates}");
			GD.Print($"Door state: {(isOpen ? "OPEN" : "CLOSED")}");

			var pathfinder = Pathfinder.Instance;
			if (pathfinder != null)
			{
				var path = pathfinder.FindPath(startCell, endCell);
				
				GD.Print($"Path found: {path.Count > 0}");
				GD.Print($"Path length: {path.Count}");
				
				if (path.Count > 0)
				{
					bool pathGoesThoughDoor = false;
					foreach (var cell in path)
					{
						if (IsDoorCell(cell))
						{
							pathGoesThoughDoor = true;
							break;
						}
					}
					
					GD.Print($"Path goes through door: {pathGoesThoughDoor}");
					
					if (isOpen && !pathGoesThoughDoor && path.Count > 3)
					{
						GD.PrintErr("WARNING: Door is open but path doesn't go through it!");
					}
					else if (!isOpen && pathGoesThoughDoor)
					{
						GD.PrintErr("ERROR: Door is closed but path goes through it!");
					}
					else
					{
						GD.Print("✓ Pathfinding working correctly!");
					}
				}
				else if (isOpen)
				{
					GD.PrintErr("WARNING: Door is open but no path found!");
				}
				else
				{
					GD.Print("✓ No path found (expected for closed door)");
				}
			}
		}
		else
		{
			GD.Print($"Not enough neighbors found for pathfinding test (found {neighbors.Count})");
		}

		GD.Print("=== END PATHFINDING TEST ===\n");
	}

	/// <summary>
	/// Visualize door cells and their connections in real-time
	/// </summary>
	public void VisualizeDoorConnections(float duration = 1.0f)
	{
		if (!_initialized) return;

		GridSystem gridSystem = GridSystem.Instance;
		if (gridSystem == null) return;

		foreach (var doorCell in _doorCells)
		{
			// Color based on door state
			Color cellColor = isOpen ? Colors.Green : Colors.Red;

			// Draw the cell
			DebugDraw3D.DrawBox(
				doorCell.worldCenter,
				Quaternion.Identity,
				Vector3.One * 0.9f,
				cellColor,
				false,
				duration
			);

			// Draw connections
			var connections = gridSystem.GetConnections(doorCell.gridCoordinates);
			foreach (var connCoord in connections)
			{
				var connCell = gridSystem.GetGridCell(connCoord);
				if (connCell != null)
				{
					DebugDraw3D.DrawLine(
						doorCell.worldCenter, 
						connCell.worldCenter, 
						cellColor, 
						duration
					);
				}
			}

			// Draw text label
			DebugDraw3D.DrawText(
				doorCell.worldCenter + Vector3.Up * 0.5f,
				$"Door: {(isOpen ? "OPEN" : "CLOSED")}\nConns: {connections.Count}"
			);
		}
	}

	/// <summary>
	/// Debug visualization in editor
	/// </summary>
	public override void _Process(double delta)
	{
		if (!_initialized || _doorCells.Count == 0) return;

		// Continuous visualization of door cells
		foreach (var doorCell in _doorCells)
		{
			Color cellColor;
			if (isOpen)
			{
				cellColor = doorCell.IsWalkable ? Colors.Green : Colors.Yellow;
			}
			else
			{
				cellColor = Colors.Red;
			}
			
			DebugDraw3D.DrawBox(
				doorCell.worldCenter, 
				Quaternion.Identity, 
				Vector3.One * 0.95f, 
				cellColor, 
				true, 
				0
			);
		}
	}

	/// <summary>
	/// Get all door cells (read-only)
	/// </summary>
	public IReadOnlyList<GridCell> GetDoorCells()
	{
		return _doorCells.AsReadOnly();
	}

	/// <summary>
	/// Get door bounds in world coordinates
	/// </summary>
	public Aabb GetDoorBounds()
	{
		if (_doorCells.Count == 0)
			return new Aabb();

		Vector3 min = _doorCells[0].worldCenter;
		Vector3 max = _doorCells[0].worldCenter;

		foreach (var cell in _doorCells)
		{
			min.X = Mathf.Min(min.X, cell.worldCenter.X);
			min.Y = Mathf.Min(min.Y, cell.worldCenter.Y);
			min.Z = Mathf.Min(min.Z, cell.worldCenter.Z);

			max.X = Mathf.Max(max.X, cell.worldCenter.X);
			max.Y = Mathf.Max(max.Y, cell.worldCenter.Y);
			max.Z = Mathf.Max(max.Z, cell.worldCenter.Z);
		}

		return new Aabb(min, max - min);
	}
}