using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class GridSystem : Manager<GridSystem>
{
	#region Variables

	[Export(PropertyHint.Layers3DPhysics)] private uint _connectionObstacleMask = 0;

	private const float EPS = 0.0001f;

	[Export] private Vector3I _gridSize;
	public Vector3I GridSize => _gridSize;
	[Export] private Vector2 _cellSize;

	public GridCell[][,] GridCells { get; private set; }
	private HashSet<CellConnection> _cellConnections;

	public GridCell[] AllGridCells
	{
		get
		{
			if (GridCells == null) return null;
			List<GridCell> gridCells = new List<GridCell>();
			for (int y = 0; y < GridCells.Length; y++)
			{
				for (int x = 0; x < GridCells[y].GetLength(0); x++)
				{
					for (int z = 0; z < GridCells[y].GetLength(1); z++)
					{
						gridCells.Add(GridCells[y][x, z]);
					}
				}
			}

			return gridCells.ToArray();
		}
	}

	[ExportCategory("Raycasting"), Export] private bool _raycastCheck;

	[Export(PropertyHint.Range, "0,90,0.1")]
	private float _maxWalkableSlopeAngle = 45.0f;

	[Export] private float _raycastDistance; // kept for compatibility
	[Export] private Vector3 _raycastOffset;

	
	[ExportCategory("Connections"),Export(PropertyHint.Range, "0.1,1.0,0.05")]
	private float _connectionBoxWidthFactor = 0.6f; // fraction of cell X

	[Export(PropertyHint.Range, "0.1,1.0,0.05")]
	private float _connectionBoxHeightFactor = 0.9f; // fraction of cell Y

	[Export(PropertyHint.Range, "0.0,0.25,0.01")]
	private float _connectionBoxEndClearance = 0.05f; // trim at ends (world units)

	[Export] private bool _allowDiagonalConnections = true;

	[Export] private bool _blockDiagonalCornerCutting = true;
	
	private Dictionary<Vector3I, HashSet<Vector3I>> _adj;
	private BoxShape3D _corridorBox;
	private PhysicsShapeQueryParameters3D _corridorParams;

// Scratch containers to reduce allocations
	private readonly List<Vector3I> _tmpNeighbors = new(32);
	private readonly List<Vector3I> _tmpRemovals = new(32);
	#endregion

	#region Signals

	[Signal]
	public delegate void GridSystemInitializedEventHandler();

	#endregion

	#region Functions

	#region Manager Functions

	protected override async Task _Setup()
	{
		var gen = MeshTerrainGenerator.Instance;
		_cellSize = gen.GetCellSize();
		_gridSize = gen.GetMapCellSize();
		await Task.CompletedTask;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		for (int y = 0; y < GridCells.Length; y++)
		{
			for (int x = 0; x < GridCells[y].GetLength(0); x++)
			{
				for (int z = 0; z < GridCells[y].GetLength(1); z++)
				{
					VisualizeCell(GridCells[y][x, z], true);
				}
			}
		}
	}

	protected override async Task _Execute()
	{
		_cellConnections = new HashSet<CellConnection>();
		_adj = new Dictionary<Vector3I, HashSet<Vector3I>>(1024);
		_corridorBox ??= new BoxShape3D();
		_corridorParams ??= new PhysicsShapeQueryParameters3D
		{
			CollideWithBodies = true,
			CollideWithAreas = false
		};

		var foundGridObjects = await SetupGrid();
		await SetupCellConnections();

		var allGridObjectsInScene = GetTree()
			.GetNodesInGroup("GridObjects")
			.OfType<GridObject>()
			.ToList();

		foreach (var go in allGridObjectsInScene)
		{
			if (!foundGridObjects.Contains(go))
			{
				foundGridObjects.Add(go);
			}
		}

		await InitializeGridObjects(foundGridObjects);

		var gridCellStateOverrides = GetTree()
			.GetNodesInGroup("GridCellOverride")
			.OfType<GridCellStateOverride>()
			.ToArray();

		for (int i = 0; i < gridCellStateOverrides.Length; i++)
		{
			gridCellStateOverrides[i].InitializeGridCellOverride();
		}

		EmitSignal(SignalName.GridSystemInitialized);
	}

	private async Task InitializeGridObjects(IEnumerable<GridObject> gridObjects)
	{
		foreach (var gridObject in gridObjects)
		{
			if (gridObject.IsInitialized) continue;

			if (TyGetGridCellFromWorldPosition(gridObject.GlobalPosition, out GridCell gridCell, true))
			{
				await gridObject.Initialize(gridObject.Team, gridCell);
			}
			else
			{
				GD.PrintErr(
					$"Could not find grid cell for GridObject {gridObject.Name} at {gridObject.GlobalPosition}");
			}
		}
	}

	#endregion

	// GridSystem.SetupGrid
	private async Task<List<GridObject>> SetupGrid()
{
    GridCells = new GridCell[_gridSize.Y][,];
    var space = GetTree().Root.GetWorld3D().DirectSpaceState;
    var foundGridObjects = new List<GridObject>();

    if (DebugMode)
    {
        DiagnoseSceneObjects();
    }

    uint groundMask = (uint)PhysicsLayer.TERRAIN;
    float maxSlopeDot = Mathf.Cos(Mathf.DegToRad(_maxWalkableSlopeAngle));
    float halfY = _cellSize.Y * 0.5f;
    float surfEps = Mathf.Max(0.001f, _cellSize.Y * 0.01f);
    if (surfEps >= halfY)
        surfEps = halfY * 0.5f;

    InventoryManager inventoryManager = InventoryManager.Instance;

    for (int y = 0; y < _gridSize.Y; y++)
    {
        GridCells[y] = new GridCell[_gridSize.X, _gridSize.Z];

        for (int x = 0; x < _gridSize.X; x++)
        {
            for (int z = 0; z < _gridSize.Z; z++)
            {
                Vector3I coords = new Vector3I(x, y, z);
                Vector3 worldCenter = new Vector3(
                    (x + 0.5f) * _cellSize.X,
                    (y + 0.5f) * _cellSize.Y,
                    -(z + 0.5f) * _cellSize.X
                );

                bool hasGround = true;

                if (_raycastCheck)
                {
                    hasGround = false;
                    bool walkableGroundFound = false;

                    // Center-only ray first, then corners only if needed
                    var rayOffsets = new List<Vector3> { Vector3.Zero };

                    var rayParams = new PhysicsRayQueryParameters3D
                    {
                        CollideWithBodies = true,
                        CollideWithAreas = false,
                        CollisionMask = groundMask
                    };

                    for (int pass = 0; pass < 2 && !hasGround; pass++)
                    {
                        if (pass == 1)
                        {
                            rayOffsets.Clear();
                            float cornerOffset = _cellSize.X * 0.45f;
                            rayOffsets.Add(new Vector3(cornerOffset, 0, cornerOffset));
                            rayOffsets.Add(new Vector3(-cornerOffset, 0, cornerOffset));
                            rayOffsets.Add(new Vector3(cornerOffset, 0, -cornerOffset));
                            rayOffsets.Add(new Vector3(-cornerOffset, 0, -cornerOffset));
                        }

                        foreach (var offset in rayOffsets)
                        {
                            Vector3 from = worldCenter
                                + _raycastOffset
                                + offset
                                + Vector3.Up * (halfY - surfEps);
                            Vector3 to = worldCenter
                                + _raycastOffset
                                + offset
                                + Vector3.Down * (halfY - surfEps);

                            rayParams.From = from;
                            rayParams.To = to;

                            var hit = space.IntersectRay(rayParams);
                            if (hit.Count > 0)
                            {
                                if (offset == Vector3.Zero)
                                {
                                    var hitY = hit["position"].As<Vector3>().Y;
                                    worldCenter.Y = hitY; // keep center at mid-height
                                }

                                hasGround = true;
                                Vector3 normal = (Vector3)hit["normal"];
                                if (normal.Dot(Vector3.Up) >= maxSlopeDot)
                                {
                                    walkableGroundFound = true;
                                    break;
                                }
                            }
                        }
                    }

                    // If only steep ground found, still mark as Ground for now
                    if (hasGround && !walkableGroundFound)
                    {
                        // extend state logic if needed later
                    }
                }

                Enums.GridCellState state = Enums.GridCellState.None;
                if (_raycastCheck)
                {
                    state |= hasGround
                        ? Enums.GridCellState.Ground
                        : Enums.GridCellState.Air;
                }

                CreateOrUpdateCell(coords, worldCenter, state, inventoryManager);
            }
        }
    }

    var gridObjects = GetTree()
        .GetNodesInGroup("GridObjects")
        .Cast<GridObject>()
        .ToList();

    foreach (GridObject gridObject in gridObjects)
    {
        gridObject.Initialize(gridObject.Team, null);
    }

    await Task.CompletedTask;
    return foundGridObjects;
}

	private void BuildAllConnections()
	{
		var space = GetTree().Root.GetWorld3D().DirectSpaceState;
		uint obstacleMask = _connectionObstacleMask != 0
			? _connectionObstacleMask
			: (uint)PhysicsLayer.OBSTACLE;

		float halfY = _cellSize.Y * 0.5f;

		_cellConnections.Clear();
		_adj.Clear();

		int totalTests = 0;
		int totalBlocked = 0;
		int cellsProcessed = 0;

		for (int y = 0; y < _gridSize.Y; y++)
		{
			for (int x = 0; x < _gridSize.X; x++)
			{
				for (int z = 0; z < _gridSize.Z; z++)
				{
					var cell = GridCells[y][x, z];
					if (cell == null)
						continue;

					cellsProcessed++;

					_tmpNeighbors.Clear();
					var (tests, blocked) = BuildConnectionsForCell(
						space,
						cell,
						obstacleMask,
						halfY,
						_tmpNeighbors
					);

					totalTests += tests;
					totalBlocked += blocked;

					ApplyConnectionsSymmetric(cell, _tmpNeighbors);
				}
			}
		}

		if (DebugMode)
		{
			GD.Print("\n=== CONNECTION RAYCAST SUMMARY ===");
			GD.Print($"Total cells processed: {cellsProcessed}");
			GD.Print($"Total connections created: {GetConnectionCount()}");
			GD.Print($"Total tests: {totalTests}");
			GD.Print($"Blocked hits: {totalBlocked}");
			float rate = totalTests > 0 ? (totalBlocked * 100.0f / totalTests) : 0;
			GD.Print($"Hit rate: {rate:F2}%");
		}
	}

	private static readonly Vector3I[] Offsets8 =
	{
		new Vector3I(0, 0, 1),   // North
		new Vector3I(1, 0, 1),   // NorthEast
		new Vector3I(1, 0, 0),   // East
		new Vector3I(1, 0, -1),  // SouthEast
		new Vector3I(0, 0, -1),  // South
		new Vector3I(-1, 0, -1), // SouthWest
		new Vector3I(-1, 0, 0),  // West
		new Vector3I(-1, 0, 1),  // NorthWest
	};

	#region Connection Management

	/// <summary>
	/// Adds a bidirectional connection between two cells.
	/// </summary>
	public void AddConnection(Vector3I cellA, Vector3I cellB)
	{
		if (cellA == cellB) return;

		var connection = new CellConnection(cellA, cellB);
		if (_cellConnections.Add(connection))
		{
			GetAdjSet(cellA, create: true).Add(cellB);
			GetAdjSet(cellB, create: true).Add(cellA);
		}
	}
	/// <summary>
	/// Removes the connection between two cells.
	/// </summary>
	public void RemoveConnection(Vector3I cellA, Vector3I cellB)
	{
		var connection = new CellConnection(cellA, cellB);
		if (_cellConnections.Remove(connection))
		{
			if (_adj != null)
			{
				if (_adj.TryGetValue(cellA, out var setA)) setA.Remove(cellB);
				if (_adj.TryGetValue(cellB, out var setB)) setB.Remove(cellA);
			}
		}
	}

	/// <summary>
	/// Gets all cells connected to the given cell.
	/// </summary>
	public List<Vector3I> GetConnections(Vector3I cell)
	{
		if (_adj != null && _adj.TryGetValue(cell, out var set))
			return new List<Vector3I>(set);

		return new List<Vector3I>();
	}

	/// <summary>
	/// Checks if two cells are connected.
	/// </summary>
	public bool AreConnected(Vector3I cellA, Vector3I cellB)
	{
		if (_adj == null) return false;
		return _adj.TryGetValue(cellA, out var set) && set.Contains(cellB);
	}

	/// <summary>
	/// Checks if a cell has any connections.
	/// </summary>
	public bool HasConnections(Vector3I cell)
	{
		if (_adj == null) return false;
		return _adj.TryGetValue(cell, out var set) && set.Count > 0;
	}

	/// <summary>
	/// Gets the total number of connections in the grid.
	/// </summary>
	public int GetConnectionCount()
	{
		return _cellConnections?.Count ?? 0;
	}

	/// <summary>
	/// Clears all connections for a specific cell.
	/// </summary>
	public void ClearConnectionsForCell(Vector3I cell)
	{
		if (_adj == null) return;

		if (_adj.TryGetValue(cell, out var set))
		{
			_tmpRemovals.Clear();
			foreach (var n in set) _tmpRemovals.Add(n);
			for (int i = 0; i < _tmpRemovals.Count; i++)
				RemoveConnection(cell, _tmpRemovals[i]);
			set.Clear();
		}
	}

	private HashSet<Vector3I> GetAdjSet(Vector3I cell, bool create)
	{
		if (_adj == null) return null;
		if (_adj.TryGetValue(cell, out var set)) return set;
		if (!create) return null;
		set = new HashSet<Vector3I>();
		_adj[cell] = set;
		return set;
	}
	
	#endregion

	private (int tests, int blockedHits) BuildConnectionsForCell(
    PhysicsDirectSpaceState3D space,
    GridCell cell,
    uint obstacleMask,
    float halfY,
    List<Vector3I> outNeighbors
)
{
    outNeighbors.Clear();

       // Treat obstructed cells as non-walkable for graph purposes
	      if (!cell.state.HasFlag(Enums.GridCellState.Ground) || cell.state.HasFlag(Enums.GridCellState.Obstructed))
		      return (0, 0);

    int tests = 0;
    int blockedHits = 0;
    Vector3I coords = cell.gridCoordinates;

    for (int dy = -1; dy <= 1; dy++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0)
                    continue;

                int manhattan = Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz);
                bool isDiagonal = manhattan > 1;
                if (!_allowDiagonalConnections && isDiagonal)
                    continue;

                Vector3I step = new Vector3I(dx, dy, dz);
                Vector3I neighborCoords3I = coords + step;
                GridCell neighbor = GetGridCell(neighborCoords3I);
                if (neighbor == null)
                    continue;

                if (!neighbor.state.HasFlag(Enums.GridCellState.Ground) || neighbor.state.HasFlag(Enums.GridCellState.Obstructed))
	                continue;

                if (isDiagonal && _blockDiagonalCornerCutting)
                {
                    bool cornerBlocked = false;

                    if (dx != 0)
                    {
                        var c = GetGridCell(coords + new Vector3I(dx, 0, 0));
                        if (c == null || !c.state.HasFlag(Enums.GridCellState.Ground))
                            cornerBlocked = true;
                        else
                        {
                            tests++;
                            if (!IsPassageClearBox(space, obstacleMask, cell, c, halfY))
                            {
                                blockedHits++;
                                cornerBlocked = true;
                            }
                        }
                    }

                    if (!cornerBlocked && dy != 0)
                    {
                        var c = GetGridCell(coords + new Vector3I(0, dy, 0));
                        if (c == null || !c.state.HasFlag(Enums.GridCellState.Ground))
                            cornerBlocked = true;
                        else
                        {
                            tests++;
                            if (!IsPassageClearBox(space, obstacleMask, cell, c, halfY))
                            {
                                blockedHits++;
                                cornerBlocked = true;
                            }
                        }
                    }

                    if (!cornerBlocked && dz != 0)
                    {
                        var c = GetGridCell(coords + new Vector3I(0, 0, dz));
                        if (c == null || !c.state.HasFlag(Enums.GridCellState.Ground))
                            cornerBlocked = true;
                        else
                        {
                            tests++;
                            if (!IsPassageClearBox(space, obstacleMask, cell, c, halfY))
                            {
                                blockedHits++;
                                cornerBlocked = true;
                            }
                        }
                    }

                    if (cornerBlocked)
                        continue;
                }

                tests++;
                if (!IsPassageClearBox(space, obstacleMask, cell, neighbor, halfY))
                {
                    blockedHits++;
                    continue;
                }

                outNeighbors.Add(neighborCoords3I);
            }
        }
    }

    return (tests, blockedHits);
}

	private void ApplyConnectionsSymmetric(GridCell cell, List<Vector3I> newNeighbors)
	{
		Vector3I cellCoords = cell.gridCoordinates;

		var oldSet = GetAdjSet(cellCoords, create: false);
		var newSet = newNeighbors.Count > 0
			? new HashSet<Vector3I>(newNeighbors)
			: null;

		_tmpRemovals.Clear();

		if (oldSet != null)
		{
			foreach (var oldNeighbor in oldSet)
			{
				if (newSet == null || !newSet.Contains(oldNeighbor))
					_tmpRemovals.Add(oldNeighbor);
			}
		}

		for (int i = 0; i < _tmpRemovals.Count; i++)
		{
			RemoveConnection(cellCoords, _tmpRemovals[i]);
		}

		if (newSet != null)
		{
			foreach (var n in newSet)
			{
				if (oldSet == null || !oldSet.Contains(n))
					AddConnection(cellCoords, n);
			}
		}
	}

	private bool IsPassageClearBox(
		PhysicsDirectSpaceState3D space,
		uint obstacleMask,
		GridCell a,
		GridCell b,
		float halfY
	)
	{
		Vector3 centerA = a.worldCenter;
		Vector3 centerB = b.worldCenter;

		Vector3 delta = centerB - centerA;
		float dist = delta.Length();
		if (dist <= 1e-4f)
			return true;

		float width = Mathf.Max(0.05f, _cellSize.X * _connectionBoxWidthFactor);
		float height = Mathf.Max(0.05f, _cellSize.Y * _connectionBoxHeightFactor);

		float trim = Mathf.Clamp(_connectionBoxEndClearance, 0f, dist * 0.45f);
		float length = Mathf.Max(0.01f, dist - trim * 2f);

		Vector3 fwd = delta / dist;
		Vector3 zAxis = -fwd;
		Vector3 refUp = Mathf.Abs(fwd.Y) > 0.95f ? Vector3.Forward : Vector3.Up;

		Vector3 xAxis = refUp.Cross(zAxis);
		float xLen = xAxis.Length();
		if (xLen <= 1e-6f)
		{
			refUp = Vector3.Right;
			xAxis = refUp.Cross(zAxis);
			xLen = xAxis.Length();
			if (xLen <= 1e-6f)
				return false;
		}
		xAxis /= xLen;
		Vector3 yAxis = zAxis.Cross(xAxis).Normalized();

		Basis basis = new Basis(xAxis, yAxis, zAxis);
		Vector3 mid = (centerA + centerB) * 0.5f;

		_corridorBox.Size = new Vector3(width, height, length);

		_corridorParams.Shape = _corridorBox;
		_corridorParams.Transform = new Transform3D(basis, mid);
		_corridorParams.CollisionMask = obstacleMask;

		// We only need to know if it hits anything
		var hits = space.IntersectShape(_corridorParams, 1);
		return hits.Count == 0;
	}

	private void MarkIsolatedCellsUnwalkable()
	{
		int isolatedCells = 0;

		for (int y = 0; y < _gridSize.Y; y++)
		{
			for (int x = 0; x < _gridSize.X; x++)
			{
				for (int z = 0; z < _gridSize.Z; z++)
				{
					GridCell cell = GridCells[y][x, z];
					if (cell == null)
						continue;
					
					if (!HasConnections(cell.gridCoordinates))
					{
						isolatedCells++;
					}
				}
			}
		}

		GD.Print($"Cells with no connections (unwalkable by rule): {isolatedCells}");
		GD.Print("=== GRID SETUP COMPLETE ===\n");
	}

	public void UpdateGridCell(Vector3I coords)
	{
		var gridCell = GetGridCell(coords);
		if (gridCell == null) return;

		var space = GetTree().Root.GetWorld3D().DirectSpaceState;

		uint obstacleMask = _connectionObstacleMask != 0
			? _connectionObstacleMask
			: (uint)PhysicsLayer.OBSTACLE;

		float halfY = _cellSize.Y * 0.5f;

		_tmpNeighbors.Clear();
		var _ = BuildConnectionsForCell(
			space,
			gridCell,
			obstacleMask,
			halfY,
			_tmpNeighbors
		);
		ApplyConnectionsSymmetric(gridCell, _tmpNeighbors);
	}

	public void UpdateNeighborsConnections(Vector3I centerCoords)
	{
		for (int yOffset = -1; yOffset <= 1; yOffset++)
		{
			for (int xOffset = -1; xOffset <= 1; xOffset++)
			{
				for (int zOffset = -1; zOffset <= 1; zOffset++)
				{
					if (xOffset == 0 && yOffset == 0 && zOffset == 0)
						continue;

					Vector3I neighborCoords =
						centerCoords + new Vector3I(xOffset, yOffset, zOffset);
					GridCell neighborCell = GetGridCell(neighborCoords);
					if (neighborCell == null)
						continue;

					UpdateGridCell(neighborCoords);
				}
			}
		}
	}

	private async Task SetupCellConnections()
	{
		BuildAllConnections();
		MarkIsolatedCellsUnwalkable();
		await Task.CompletedTask;
	}

	private void DiagnoseSceneObjects()
	{
		GD.Print("\n=== SCENE OBJECT DIAGNOSIS ===");

		var staticBodies = GetTree().GetNodesInGroup("all").OfType<StaticBody3D>().ToList();
		if (staticBodies.Count == 0)
		{
			staticBodies = FindAllNodesOfType<StaticBody3D>(GetTree().Root);
		}

		GD.Print($"Found {staticBodies.Count} StaticBody3D objects in scene");

		foreach (var body in staticBodies.Take(5))
		{
			GD.Print($"  - {body.Name}");
			GD.Print($"    Position: {body.GlobalPosition}");
			GD.Print(
				$"    Collision Layer: {body.CollisionLayer} (binary: {Convert.ToString(body.CollisionLayer, 2).PadLeft(32, '0')})");
			GD.Print($"    Collision Mask: {body.CollisionMask}");

			var shapes = body.GetChildren().OfType<CollisionShape3D>().ToList();
			GD.Print($"    Has {shapes.Count} CollisionShape3D children");
		}

		var allCollisionObjects = FindAllNodesOfType<CollisionObject3D>(GetTree().Root);
		GD.Print($"\nTotal CollisionObject3D in scene: {allCollisionObjects.Count}");

		var layerGroups = allCollisionObjects
			.GroupBy(obj => obj.CollisionLayer)
			.OrderBy(g => g.Key);

		foreach (var group in layerGroups)
		{
			GD.Print(
				$"  Layer {group.Key} (binary: {Convert.ToString(group.Key, 2).PadLeft(8, '0')}): {group.Count()} objects");
			foreach (var obj in group.Take(3))
			{
				GD.Print($"    - {obj.Name} ({obj.GetType().Name})");
			}
		}

		GD.Print("=== END DIAGNOSIS ===\n");
	}

	private List<T> FindAllNodesOfType<T>(Node root) where T : Node
	{
		var results = new List<T>();
		FindNodesRecursive(root, results);
		return results;
	}

	private void FindNodesRecursive<T>(Node node, List<T> results) where T : Node
	{
		if (node is T match)
			results.Add(match);

		foreach (Node child in node.GetChildren())
		{
			FindNodesRecursive(child, results);
		}
	}

	public GridCell GetGridCell(Vector3I position)
	{
		if (GridCells == null)
			return null;
		if (position.Y < 0 || position.Y >= GridCells.Length)
			return null;
		if (
			position.X < 0
			|| position.X >= GridCells[position.Y].GetLength(0)
		)
			return null;
		if (
			position.Z < 0
			|| position.Z >= GridCells[position.Y].GetLength(1)
		)
			return null;
		return GridCells[position.Y][position.X, position.Z];
	}

	private void CreateOrUpdateCell(
		Vector3I coords,
		Vector3 position,
		Enums.GridCellState state,
		InventoryManager inventoryManager
	)
	{
		if (GridCells[coords.Y][coords.X, coords.Z] == null)
		{
			GridCells[coords.Y][coords.X, coords.Z] = new GridCell(
				coords,
				position,
				state,
				Enums.FogState.Unseen,
				inventoryManager.GetInventoryGrid(Enums.InventoryType.Ground)
				
			);

		}
		else
		{
			GridCell cell = GridCells[coords.Y][coords.X, coords.Z];
			cell.SetState(state);
			cell.SetWorldCenter(position);
		}
	}

	public bool TyGetGridCellFromWorldPosition(
		Vector3 worldPosition,
		out GridCell gridCoords,
		bool nullGetNearest
	)
	{
		gridCoords = null;

		if (_cellSize.X <= 0f || _cellSize.Y <= 0f || GridCells == null)
			return false;

		int X = Mathf.FloorToInt(worldPosition.X / _cellSize.X);

		// Now that worldCenter is the actual center, adjust accordingly
		float ny = worldPosition.Y / _cellSize.Y;
		int Y = Mathf.FloorToInt(ny);

		// Negative Z forward: use -worldPosition.Z
		float nz = -worldPosition.Z / _cellSize.X;
		int Z = Mathf.FloorToInt(nz);

		Y = Mathf.Clamp(Y, 0, GridCells.Length - 1);
		if (Y < 0 || Y >= GridCells.Length)
			return false;

		X = Mathf.Clamp(X, 0, GridCells[Y].GetLength(0) - 1);
		Z = Mathf.Clamp(Z, 0, GridCells[Y].GetLength(1) - 1);

		gridCoords = GridCells[Y][X, Z];
		if (gridCoords != null)
			return true;

		if (!nullGetNearest)
			return false;

		float minDistance = float.MaxValue;
		GridCell nearest = null;
		int rows = GridCells[Y].GetLength(0);
		int cols = GridCells[Y].GetLength(1);

		for (int i = 0; i < rows; i++)
		{
			for (int j = 0; j < cols; j++)
			{
				GridCell candidate = GridCells[Y][i, j];
				if (candidate != null)
				{
					float distSq = (candidate.worldCenter - worldPosition)
						.LengthSquared();
					if (distSq < minDistance)
					{
						minDistance = distSq;
						nearest = candidate;
					}
				}
			}
		}

		if (nearest != null)
		{
			gridCoords = nearest;
			return true;
		}

		return false;
	}

	public bool TryGetRandomGridCell(
		bool onlyWalkable,
		out GridCell gridCell,
		Enums.GridCellState stateFilter = Enums.GridCellState.None,
		bool onlyNonOccupied = true,
		Enums.UnitTeam teamFilter = Enums.UnitTeam.None,
		bool excludeTeam = false
	)
	{
		gridCell = null;
		int seen = 0;

		for (int y = 0; y < _gridSize.Y; y++)
		{
			var layer = GridCells[y];
			int rows = layer.GetLength(0);
			int cols = layer.GetLength(1);

			for (int x = 0; x < rows; x++)
			{
				for (int z = 0; z < cols; z++)
				{
					var candidate = layer[x, z];
					if (candidate == null) continue;

					if (onlyWalkable && !candidate.IsWalkable) continue;

					if (
						stateFilter != Enums.GridCellState.None
						&& !candidate.state.HasFlag(stateFilter)
					) continue;

					if (onlyNonOccupied && candidate.HasGridObject()) continue;

					if (teamFilter != Enums.UnitTeam.None)
					{
						if (candidate.UnitTeamSpawn == Enums.UnitTeam.None)
							continue;

						if (excludeTeam)
						{
							if (candidate.UnitTeamSpawn.HasFlag(teamFilter))
								continue;
						}
						else
						{
							if (candidate.UnitTeamSpawn != teamFilter)
								continue;
						}
					}

					seen++;
					if (gridCell == null || GD.RandRange(0, seen - 1) < 1)
					{
						gridCell = candidate;
					}
				}
			}
		}

		if (gridCell != null) return true;

		GD.Print("Failed to find a suitable cell.");
		return false;
	}
	public void VisualizeCell(GridCell gridCell, bool hideAir = true, int duration = -1)
	{
		var s = gridCell.state;

		if (hideAir && s.HasFlag(Enums.GridCellState.Air) && !s.HasFlag(Enums.GridCellState.Ground))
			return;

		bool isWalkable = gridCell.IsWalkable;

		Color color = new Color(1f, 1f, 1f, 1f);

		if (s.HasFlag(Enums.GridCellState.Obstructed))
		{
			color = new Color(1f, 0f, 0f, 1f);
		}
		else if (isWalkable)
		{
			color = s.HasFlag(Enums.GridCellState.Ground)
				? new Color(0f, 1f, 0f, 1f)
				: new Color(0.5f, 1f, 0.5f, 1f);
		}
		else if (s.HasFlag(Enums.GridCellState.Ground))
		{
			color = new Color(1f, 1f, 0f, 1f);
		}

		// Draw connections
		var connections = GetConnections(gridCell.gridCoordinates);
		foreach (var neighborCoords in connections)
		{
			GridCell neighbor = GetGridCell(neighborCoords);
			if (neighbor == null)
				continue;

			DebugDraw3D.DrawLine(gridCell.worldCenter, neighbor.worldCenter, color);
		}
		
		DebugDraw3D.DrawBox(
			gridCell.worldCenter,
			Quaternion.Identity,
			new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X),
			color,
			true,
			duration != -1 ? duration : 0
		);
	}

	public bool TryGetGridCellNeighbors(
		GridCell startingGridCell, 
		bool onlyWalkable, 
		bool sameLevelOnly,
		out List<GridCell> neighbors
	)
	{
		neighbors = new List<GridCell>();
		if (startingGridCell == null)
			return false;

		if (onlyWalkable)
		{
			var connectedCoords = GetConnections(startingGridCell.gridCoordinates);
			foreach (var coords in connectedCoords)
			{
				if (sameLevelOnly && coords.Y != startingGridCell.gridCoordinates.Y)
					continue;
                
				var n = GetGridCell(coords);
				if (n != null)
					neighbors.Add(n);
			}
		}
		else
		{
			for (int y = -1; y <= 1; y++)
			{
				for (int x = -1; x <= 1; x++)
				{
					for (int z = -1; z <= 1; z++)
					{
						if (sameLevelOnly && y != 0) continue;
						if (x == 0 && y == 0 && z == 0) continue;
                    
						GridCell tempCell = GetGridCell(
							new Vector3I(x, y, z) + startingGridCell.gridCoordinates
						);
						if (tempCell == null) continue;

						neighbors.Add(tempCell);
					}
				}
			}
		}

		return neighbors.Count > 0;
	}

	public bool TryGetGridCellsInRange(
		GridCell startingGridCell,
		Vector2I range,
		bool onlyWalkable,
		out List<GridCell> neighbors,
		Enums.GridCellState stateFilter = Enums.GridCellState.None
	)
	{
		neighbors = new List<GridCell>();
		if (startingGridCell == null)
			return false;
		if (range.X < 1 || range.Y < 1)
			return false;

		for (int y = Mathf.RoundToInt(-range.Y / 2.0); y < Mathf.RoundToInt(range.Y / 2.0); y++)
		{
			for (int x = Mathf.RoundToInt(-range.X / 2.0); x < Mathf.RoundToInt(range.X / 2.0); x++)
			{
				for (int z = Mathf.RoundToInt(-range.X / 2.0); z < Mathf.RoundToInt(range.X / 2.0); z++)
				{
					Vector3I gridCellCoords = startingGridCell.gridCoordinates + new Vector3I(x, y, z);
					GridCell gridCell = GetGridCell(gridCellCoords);
					if (gridCell == null)
						continue;
					
					if(onlyWalkable && !gridCell.IsWalkable) continue;
					
					if (stateFilter != Enums.GridCellState.None && !gridCell.state.HasFlag(stateFilter))
						continue;
					neighbors.Add(gridCell);
				}
			}
		}

		return neighbors.Count > 0;
	}

	public bool TryGetGridCellsInArea(
		Area3D area,
		out List<GridCell> gridCells
	)
	{
		gridCells = new List<GridCell>();
		if (GridCells == null || area == null)
			return false;

		if (!TryGetAreaWorldAabb(area, out Aabb areaAabb))
			return false;

		Vector3 min = areaAabb.Position;
		Vector3 max = areaAabb.End;

		int minX = Mathf.FloorToInt(min.X / _cellSize.X);
		int maxX = Mathf.FloorToInt((max.X - EPS) / _cellSize.X);

		// Negative Z forward mapping
		int minZ = Mathf.FloorToInt((-(max.Z - EPS)) / _cellSize.X);
		int maxZ = Mathf.FloorToInt((-min.Z) / _cellSize.X);
		if (minZ > maxZ)
		{
			int tmpZ = minZ;
			minZ = maxZ;
			maxZ = tmpZ;
		}

		int minY = Mathf.FloorToInt(min.Y / _cellSize.Y);
		int maxY = Mathf.FloorToInt((max.Y - EPS) / _cellSize.Y);

		if (
			!ClampIndexRange(ref minX, ref maxX, _gridSize.X)
			|| !ClampIndexRange(ref minY, ref maxY, _gridSize.Y)
			|| !ClampIndexRange(ref minZ, ref maxZ, _gridSize.Z)
		)
		{
			return false;
		}

		float halfY = _cellSize.Y * 0.5f;

		var space = GetTree().Root.GetWorld3D().DirectSpaceState;
		var pointParams = new PhysicsPointQueryParameters3D
		{
			CollideWithAreas = true,
			CollideWithBodies = false,
			CollisionMask = area.CollisionLayer
		};

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				for (int z = minZ; z <= maxZ; z++)
				{
					var cell = GridCells[y][x, z];
					if (cell == null)
						continue;

					// Build AABB from cell center
					Vector3 half = new Vector3(
						_cellSize.X * 0.5f,
						halfY,
						_cellSize.X * 0.5f
					);
					Vector3 aabbMin = cell.worldCenter - half;
					Vector3 aabbSize = new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X);
					var cellAabb = new Aabb(aabbMin, aabbSize);

					if (!areaAabb.Intersects(cellAabb))
						continue;

					// Sample at cell center
					pointParams.Position = cell.worldCenter;

					var hits = space.IntersectPoint(pointParams, 8);
					bool inside = false;
					foreach (Godot.Collections.Dictionary hit in hits)
					{
						var collider = hit["collider"].As<GodotObject>();
						if (collider == area)
						{
							inside = true;
							break;
						}
					}

					if (inside)
						gridCells.Add(cell);
				}
			}
		}

		return gridCells.Count > 0;
	}

	private static bool TryGetAreaWorldAabb(Area3D area, out Aabb aabb)
	{
		aabb = default;
		var shapes = new List<CollisionShape3D>();
		CollectCollisionShapes(area, shapes);
		if (shapes.Count == 0)
			return false;

		bool initialized = false;
		foreach (var cs in shapes)
		{
			var shape = cs.Shape;
			if (shape == null)
				continue;

			Aabb local;
			switch (shape)
			{
				case BoxShape3D box:
				{
					Vector3 size = box.Size;
					local = new Aabb(-size * 0.5f, size);
					break;
				}
				case SphereShape3D sphere:
				{
					float r = sphere.Radius;
					Vector3 ext = new Vector3(r, r, r);
					local = new Aabb(-ext, ext * 2f);
					break;
				}
				case CapsuleShape3D cap:
				{
					float r = cap.Radius;
					float h = cap.Height;
					Vector3 ext = new Vector3(r, h * 0.5f + r, r);
					local = new Aabb(-ext, ext * 2f);
					break;
				}
				case CylinderShape3D cyl:
				{
					float r = cyl.Radius;
					float h = cyl.Height;
					Vector3 ext = new Vector3(r, h * 0.5f, r);
					local = new Aabb(-ext, ext * 2f);
					break;
				}
				default:
				{
					var dbg = shape.GetDebugMesh();
					if (dbg == null)
						continue;
					local = dbg.GetAabb();
					break;
				}
			}

			Aabb worldAabb = TransformAabb(cs.GlobalTransform, local);
			aabb = initialized ? aabb.Merge(worldAabb) : worldAabb;
			initialized = true;
		}

		return initialized;
	}

	private static void CollectCollisionShapes(
		Node node,
		List<CollisionShape3D> output
	)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is CollisionShape3D cs && cs.Shape != null)
				output.Add(cs);

			if (child.GetChildCount() > 0)
				CollectCollisionShapes(child, output);
		}
	}

	private static Aabb TransformAabb(Transform3D xf, Aabb local)
	{
		Vector3 minLocal = local.Position;
		Vector3 maxLocal = local.Position + local.Size;

		Vector3 min = new Vector3(
			float.PositiveInfinity,
			float.PositiveInfinity,
			float.PositiveInfinity
		);
		Vector3 max = new Vector3(
			float.NegativeInfinity,
			float.NegativeInfinity,
			float.NegativeInfinity
		);

		for (int xi = 0; xi < 2; xi++)
		{
			float x = (xi == 0) ? minLocal.X : maxLocal.X;
			for (int yi = 0; yi < 2; yi++)
			{
				float y = (yi == 0) ? minLocal.Y : maxLocal.Y;
				for (int zi = 0; zi < 2; zi++)
				{
					float z = (zi == 0) ? minLocal.Z : maxLocal.Z;
					Vector3 p = xf * new Vector3(x, y, z);
					min.X = Mathf.Min(min.X, p.X);
					min.Y = Mathf.Min(min.Y, p.Y);
					min.Z = Mathf.Min(min.Z, p.Z);
					max.X = Mathf.Max(max.X, p.X);
					max.Y = Mathf.Max(max.Y, p.Y);
					max.Z = Mathf.Max(max.Z, p.Z);
				}
			}
		}

		return new Aabb(min, max - min);
	}

	private static bool ClampIndexRange(ref int min, ref int max, int count)
	{
		if (count <= 0)
			return false;

		min = Mathf.Clamp(min, 0, count - 1);
		max = Mathf.Clamp(max, 0, count - 1);
		if (max < min)
			return false;

		return true;
	}

	public GridCell GetCellInDirection(GridCell startCell, Enums.Direction direction)
	{
		if (startCell == null || direction == Enums.Direction.None)
		{
			return null;
		}

		Vector3I offset = GetDirectionOffset(direction);
		Vector3I targetCoords = startCell.gridCoordinates + offset;

		return GetGridCell(targetCoords);
	}

	private Vector3I GetDirectionOffset(Enums.Direction direction)
	{
		switch (direction)
		{
			case Enums.Direction.North:
				return new Vector3I(0, 0, 1);
			case Enums.Direction.NorthEast:
				return new Vector3I(1, 0, 1);
			case Enums.Direction.East:
				return new Vector3I(1, 0, 0);
			case Enums.Direction.SouthEast:
				return new Vector3I(1, 0, -1);
			case Enums.Direction.South:
				return new Vector3I(0, 0, -1);
			case Enums.Direction.SouthWest:
				return new Vector3I(-1, 0, -1);
			case Enums.Direction.West:
				return new Vector3I(-1, 0, 0);
			case Enums.Direction.NorthWest:
				return new Vector3I(-1, 0, 1);
			default:
				return Vector3I.Zero;
		}
	}

	
	#region manager Data
	protected override void GetInstanceData(ManagerData data)
	{
		GD.Print("No data to transfer");
	}

	public override ManagerData SetInstanceData()
	{
		return null;
	}
	#endregion
	
	#endregion
}