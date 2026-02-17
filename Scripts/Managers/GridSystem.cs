using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Managers;

[Tool]
[GlobalClass]
public partial class GridSystem : Manager<GridSystem>
{
	#region Variables

	[ExportCategory("Grid Alignment")] [Export]
	private Vector3 _gridWorldOrigin = Vector3.Zero;

	public Vector3 GridWorldOrigin => _gridWorldOrigin;

	[Export(PropertyHint.Layers3DPhysics)] private uint _connectionObstacleMask = 0;

	private const float EPS = 0.0001f;

	[Export] private Vector3I _gridSize;
	public Vector3I GridSize => _gridSize;
	[Export] private Vector2 _cellSize;
	public Vector2 CellSize => _cellSize;

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
	
	[Export] private Vector3 _raycastOffset;


	[ExportCategory("Connections"), Export(PropertyHint.Range, "0.1,1.0,0.05")]
	private float _connectionBoxWidthFactor = 0.6f;

	[Export(PropertyHint.Range, "0.1,1.0,0.05")]
	private float _connectionBoxHeightFactor = 0.9f; 

	[Export(PropertyHint.Range, "0.0,0.25,0.01")]
	private float _connectionBoxEndClearance = 0.05f; 

	[Export] private bool _allowDiagonalConnections = true;

	[Export] private bool _blockDiagonalCornerCutting = true;

	
	[Export]
	public bool ExportConfiguration
	{
		get => false;
		set
		{
			if (value) ExportEditorConfiguration();
		}
	}

	private Dictionary<Vector3I, HashSet<Vector3I>> _adj;
	private BoxShape3D _corridorBox;
	private PhysicsShapeQueryParameters3D _corridorParams;
	
	
	private readonly List<Vector3I> _tmpNeighbors = new(32);
	private readonly List<Vector3I> _tmpRemovals = new(32);

	#endregion

	#region Signals
	[Signal]
	public delegate void GridSystemInitializedEventHandler();

	#endregion

	#region Functions

	#region Manager Functions

	public override string GetManagerName() => "GridSystem";

	protected override async Task _Setup(bool loadingData)
	{
		
		if (MeshTerrainGenerator.Instance != null)
		{
			var gen = MeshTerrainGenerator.Instance;
			_cellSize = gen.GetCellSize();
			_gridSize = gen.GetMapCellSize();
		}
		else
		{
			GD.PrintErr("GridSystem: MeshTerrainGenerator Instance is null during Setup.");
		}

		await Task.CompletedTask;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		base._Process(delta);
		if (DebugMode && GridCells != null)
		{
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
	}

	protected override async Task _Execute(bool loadingData)
	{
		try
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

			// Gather all grid objects
			var allGridObjectsInScene = GetTree()
				.GetNodesInGroup("GridObjects")
				.OfType<GridObject>()
				.ToList();

			foreach (var go in allGridObjectsInScene)
			{
				if (!foundGridObjects.Contains(go))
					foundGridObjects.Add(go);
			}

			// Initialize GridObjects 
			if (foundGridObjects != null && foundGridObjects.Count > 0)
				await InitializeGridObjects(foundGridObjects);

			// Apply State Overrides before building connections
			var gridCellStateOverrides = GetTree()
				.GetNodesInGroup("GridCellOverride")
				.OfType<GridCellStateOverride>()
				.ToArray();

			GD.Print($"Found {gridCellStateOverrides.Length} GridCellStateOverrides");

			foreach (var stateOverride in gridCellStateOverrides)
			{
				stateOverride.InitializeGridCellOverride();
			}

			//setup connections
			await SetupCellConnections();

			GD.Print("GridSystem: Initialization Complete.");
			EmitSignal(SignalName.GridSystemInitialized);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ERROR in GridSystem Execute: {ex.Message}\n{ex.StackTrace}");
		}


		if (DebugMode)
		{
			int obstructedCount = 0;
			int obstructedWithNoConnections = 0;

			foreach (var cell in AllGridCells)
			{
				if (cell.state.HasFlag(Enums.GridCellState.Obstructed))
				{
					obstructedCount++;
					if (!HasConnections(cell.GridCoordinates))
						obstructedWithNoConnections++;
				}
			}

			GD.Print($"=== OBSTACLE DEBUG ===");
			GD.Print($"Obstructed cells: {obstructedCount}");
			GD.Print($"Obstructed cells with no connections: {obstructedWithNoConnections}");
		}

		await Task.CompletedTask;
	}

	private async Task InitializeGridObjects(IEnumerable<GridObject> gridObjects)
	{
		foreach (var gridObject in gridObjects)
		{
			if (gridObject == null || gridObject.IsInitialized) continue;

			if (TryGetGridCellFromWorldPosition(gridObject.GlobalPosition, out GridCell gridCell, true))
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


	private async Task<List<GridObject>> SetupGrid()
	{
		GridCells = new GridCell[_gridSize.Y][,];
		var space = GetTree().Root.GetWorld3D().DirectSpaceState;
		
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager == null)
		{
			GD.PrintErr(
				"GridSystem: InventoryManager is NULL inside SetupGrid. Ensure InventoryManager runs Setup/Execute before GridSystem.");
		}

		uint groundMask = (uint)PhysicsLayer.TERRAIN;
		float maxSlopeDot = Mathf.Cos(Mathf.DegToRad(_maxWalkableSlopeAngle));

		float halfY = _cellSize.Y * 0.5f;

		float surfEps = Mathf.Max(0.001f, _cellSize.Y * 0.01f);

		InventoryGrid groundInv = null;
		if (inventoryManager != null)
		{
			groundInv = inventoryManager.GetInventoryGrid(Enums.InventoryType.Ground);

			if (groundInv == null)
			{
				GD.Print("ERROR: GridSystem: groundInv is NULL inside SetupGrid.");
			}

			GD.Print("Ground Inventory Found!");
		}

		for (int y = 0; y < _gridSize.Y; y++)
		{
			GridCells[y] = new GridCell[_gridSize.X, _gridSize.Z];

			for (int x = 0; x < _gridSize.X; x++)
			{
				for (int z = 0; z < _gridSize.Z; z++)
				{
					Vector3I coords = new Vector3I(x, y, z);

					Vector3 trueCenter = _gridWorldOrigin + new Vector3(
						(x + 0.5f) * _cellSize.X,
						(y + 0.5f) * _cellSize.Y,
						(z + 0.5f) * _cellSize.X
					);

					Vector3 WorldCenter = trueCenter;

					bool hasGround = true;

					if (_raycastCheck)
					{
						hasGround = false;
						bool walkableGroundFound = false;

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
								Vector3 from = trueCenter
								               + _raycastOffset
								               + offset
								               + Vector3.Up * (halfY + _cellSize.Y * 0.1f);

								Vector3 to = trueCenter
								             + _raycastOffset
								             + offset
								             + Vector3.Down * (halfY + surfEps);

								rayParams.From = from;
								rayParams.To = to;

								var hit = space.IntersectRay(rayParams);
								if (hit.Count > 0)
								{
									var hitPos = hit["position"].As<Vector3>();
									
									float cellTopY = trueCenter.Y + halfY + 0.01f;
									if (hitPos.Y > cellTopY)
										continue;

									if (offset == Vector3.Zero || !hasGround)
									{
										WorldCenter.Y = hitPos.Y;
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
					}

					Enums.GridCellState state = Enums.GridCellState.None;

					if (hasGround)
					{
						state |= Enums.GridCellState.Ground;
					}
					else
					{
						state |= Enums.GridCellState.Air;
					}

					CreateOrUpdateCell(coords, WorldCenter, trueCenter, state,
						(InventoryGrid)groundInv.Duplicate(true));
				}
			}
		}

		// Gather existing objects in the group to return
		var potentialGridObjects = GetTree()
			.GetNodesInGroup("GridObjects").ToList();

		var gridObjects = new List<GridObject>();

		foreach (var potentialGridObject in potentialGridObjects)
		{
			if (potentialGridObject is GridObject gridObject)
			{
				gridObjects.Add(gridObject);
			}
			else
			{
				Node currentNode = potentialGridObject;
				int maxParentChecks = 2;
				int parentLevel = 0;
				while (currentNode != null && parentLevel <= maxParentChecks)
				{
					if (currentNode is GridObject go)
					{
						gridObjects.Add(go);
						break;
					}

					currentNode = currentNode.GetParent();
					parentLevel++;
				}
			}
		}

		await Task.CompletedTask;
		return gridObjects;
	}

	private void CreateOrUpdateCell(
		Vector3I coords,
		Vector3 position,
		Vector3 trueCenter,
		Enums.GridCellState state,
		InventoryGrid groundInventory
	)
	{
		if (groundInventory == null)
		{
			GD.Print("ERROR: Ground inventory is null");
			return;
		}

		if (GridCells[coords.Y][coords.X, coords.Z] == null)
		{
			GridCells[coords.Y][coords.X, coords.Z] = new GridCell(
				coords,
				position,
				new Vector3(coords.X, coords.Y, coords.Z) * new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X),
				state,
				Enums.FogState.Unseen,
				groundInventory
			);
		}
		else
		{
			GridCell cell = GridCells[coords.Y][coords.X, coords.Z];
			cell.SetState(state);
			cell.SetWorldCenter(position);
			if (cell.InventoryGrid == null && groundInventory != null)
			{
				cell.SetInventory(groundInventory);
			}
		}
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

	#region Connection Management

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

	public List<Vector3I> GetConnections(Vector3I cell)
	{
		if (_adj != null && _adj.TryGetValue(cell, out var set))
			return new List<Vector3I>(set);

		return new List<Vector3I>();
	}

	public bool AreConnected(Vector3I cellA, Vector3I cellB)
	{
		if (_adj == null) return false;
		return _adj.TryGetValue(cellA, out var set) && set.Contains(cellB);
	}

	public bool HasConnections(Vector3I cell)
	{
		if (_adj == null) return false;
		return _adj.TryGetValue(cell, out var set) && set.Count > 0;
	}

	public int GetConnectionCount()
	{
		return _cellConnections?.Count ?? 0;
	}

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

	private (int tests, int blockedHits) BuildConnectionsForCell(PhysicsDirectSpaceState3D space, GridCell cell,
		uint obstacleMask, float halfY, List<Vector3I> outNeighbors
	)
	{
		outNeighbors.Clear();

		if (!cell.state.HasFlag(Enums.GridCellState.Ground) || cell.state.HasFlag(Enums.GridCellState.Obstructed))
			return (0, 0);

		int tests = 0;
		int blockedHits = 0;
		Vector3I coords = cell.GridCoordinates;

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

					if (!neighbor.state.HasFlag(Enums.GridCellState.Ground) ||
					    neighbor.state.HasFlag(Enums.GridCellState.Obstructed))
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
		Vector3I cellCoords = cell.GridCoordinates;

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

	private bool IsPassageClearBox(PhysicsDirectSpaceState3D space, uint obstacleMask,
		GridCell a, GridCell b, float halfY
	)
	{
		Vector3 centerA = a.WorldCenter;
		Vector3 centerB = b.WorldCenter;

		float verticalOffset = _cellSize.Y * 0.35f;
		centerA.Y += verticalOffset;
		centerB.Y += verticalOffset;

		Vector3 delta = centerB - centerA;
		float dist = delta.Length();
		if (dist <= 1e-4f) return true;

		float width = Mathf.Max(0.05f, _cellSize.X * _connectionBoxWidthFactor * 0.8f);
		float height = Mathf.Max(0.05f, _cellSize.Y * _connectionBoxHeightFactor * 0.8f);

		float trim = Mathf.Clamp(_connectionBoxEndClearance, 0f, dist * 0.45f);
		float length = Mathf.Max(0.01f, dist - trim * 2f);

		Vector3 fwd = delta / dist;
		Vector3 zAxis = -fwd;

		Vector3 refUp = Vector3.Up;
		if (Mathf.Abs(fwd.Dot(Vector3.Up)) > 0.95f)
			refUp = Vector3.Forward;

		Vector3 xAxis = refUp.Cross(zAxis).Normalized();
		Vector3 yAxis = zAxis.Cross(xAxis).Normalized();

		Basis basis = new Basis(xAxis, yAxis, zAxis);
		Vector3 mid = (centerA + centerB) * 0.5f;

		_corridorBox.Size = new Vector3(width, height, length);

		_corridorParams.Shape = _corridorBox;
		_corridorParams.Transform = new Transform3D(basis, mid);
		_corridorParams.CollisionMask = obstacleMask;

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

					if (!HasConnections(cell.GridCoordinates))
					{
						isolatedCells++;
					}
				}
			}
		}

		if (DebugMode) GD.Print($"Cells with no connections (unwalkable by rule): {isolatedCells}");
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

	public bool TryGetGridCellFromWorldPosition(
		Vector3 worldPosition,
		out GridCell gridCoords,
		bool nullGetNearest
	)
	{
		gridCoords = null;

		if (_cellSize.X <= 0f || _cellSize.Y <= 0f || GridCells == null)
			return false;

		Vector3 localPos = worldPosition - _gridWorldOrigin;

		int X = Mathf.FloorToInt(localPos.X / _cellSize.X);
		int Y = Mathf.FloorToInt(localPos.Y / _cellSize.Y);
		int Z = Mathf.FloorToInt(localPos.Z / _cellSize.X);

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
					float distSq = (candidate.WorldCenter - worldPosition)
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
		List<GridCell> cells = new List<GridCell>();

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

					cells.Add(candidate);
				}
			}
		}

		int randomIndex = cells.Count > 0 ? GD.RandRange(0, cells.Count - 1) : -1;
		if (randomIndex == -1)
		{
			gridCell = null;
			GD.Print($"Failed to find a suitable cell.");
			return false;
		}
		else
		{
			gridCell = cells[randomIndex];
			return true;
		}
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
		var connections = GetConnections(gridCell.GridCoordinates);
		foreach (var neighborCoords in connections)
		{
			GridCell neighbor = GetGridCell(neighborCoords);
			if (neighbor == null)
				continue;

			DebugDraw3D.DrawLine(gridCell.WorldCenter, neighbor.WorldCenter, color);
		}

		DebugDraw3D.DrawBox(
			gridCell.WorldCenter - new Vector3(0, .5f, 0),
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
			var connectedCoords = GetConnections(startingGridCell.GridCoordinates);
			foreach (var coords in connectedCoords)
			{
				if (sameLevelOnly && coords.Y != startingGridCell.GridCoordinates.Y)
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
							new Vector3I(x, y, z) + startingGridCell.GridCoordinates
						);
						if (tempCell == null) continue;

						neighbors.Add(tempCell);
					}
				}
			}
		}

		return neighbors.Count > 0;
	}

	public bool TryGetGridCellsNeighbors(
		List<GridCell> startingGridCells,
		bool onlyWalkable,
		bool sameLevelOnly,
		out List<GridCell> neighbors
	)
	{
		neighbors = new List<GridCell>();
		if (startingGridCells == null || startingGridCells.Count == 0)
			return false;


		foreach (var startingGridCell in startingGridCells)
		{
			if (onlyWalkable)
			{
				var connectedCoords = GetConnections(startingGridCell.GridCoordinates);
				foreach (var coords in connectedCoords)
				{
					if (sameLevelOnly && coords.Y != startingGridCell.GridCoordinates.Y)
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
								new Vector3I(x, y, z) + startingGridCell.GridCoordinates
							);
							if (tempCell == null) continue;

							neighbors.Add(tempCell);
						}
					}
				}
			}
		}

		return neighbors.Count > 0;
	}

	public List<GridCell> GetGridCellsInCone(
		GridCell startCell,
		Vector3 direction,
		float maxDistance,
		Vector2 angles,
		bool includeOrigin = false
	)
	{
		List<GridCell> result = new List<GridCell>();

		if (startCell == null || maxDistance <= 0) return result;

		direction = direction.Normalized();
		
		float halfAngleRadX = Mathf.DegToRad(angles.X * 0.5f);
		float halfAngleRadY = Mathf.DegToRad(angles.Y * 0.5f);

		float tanX = Mathf.Tan(halfAngleRadX);
		float tanY = Mathf.Tan(halfAngleRadY);

		// Calculate Basis Vectors
		Vector3 upRef = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up;
		Vector3 coneRight = direction.Cross(upRef).Normalized();
		Vector3 coneUp = coneRight.Cross(direction).Normalized();

		Vector3 originPos = startCell.WorldCenter;
		
		float minCellDim = Mathf.Min(_cellSize.X, _cellSize.Y);
		int cellRange = Mathf.CeilToInt(maxDistance / minCellDim);

		Vector3I startCoords = startCell.GridCoordinates;

		int minX = Mathf.Max(0, startCoords.X - cellRange);
		int maxX = Mathf.Min(_gridSize.X - 1, startCoords.X + cellRange);
		int minY = Mathf.Max(0, startCoords.Y - cellRange);
		int maxY = Mathf.Min(_gridSize.Y - 1, startCoords.Y + cellRange);
		int minZ = Mathf.Max(0, startCoords.Z - cellRange);
		int maxZ = Mathf.Min(_gridSize.Z - 1, startCoords.Z + cellRange);

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				for (int z = minZ; z <= maxZ; z++)
				{
					if (!includeOrigin && x == startCoords.X && y == startCoords.Y && z == startCoords.Z)
						continue;

					GridCell candidate = GridCells[y][x, z];
					if (candidate == null) continue;

					Vector3 toCandidate = candidate.WorldCenter - originPos;

					float distForward = toCandidate.Dot(direction);

					if (distForward < 0.001f || distForward > maxDistance)
						continue; 
					
					float allowedDistX = distForward * tanX;
					float allowedDistY = distForward * tanY;

					float offsetX = Mathf.Abs(toCandidate.Dot(coneRight));
					float offsetY = Mathf.Abs(toCandidate.Dot(coneUp));
					
					if (offsetX <= allowedDistX && offsetY <= allowedDistY)
					{
						result.Add(candidate);
					}
				}
			}
		}

		return result;
	}

	public List<GridCell> GetGridCellsInCone(
		GridCell startCell,
		Enums.Direction direction,
		float maxDistance,
		Vector2 angles,
		bool includeOrigin = false
	)
	{
		Vector3 dirVec = Vector3.Forward;

		switch (direction)
		{
			case Enums.Direction.North: dirVec = Vector3.Back; break;
			case Enums.Direction.South: dirVec = Vector3.Forward; break;
			case Enums.Direction.East: dirVec = Vector3.Right; break;
			case Enums.Direction.West: dirVec = Vector3.Left; break;
			default:
				Vector3I offset = GetDirectionOffset(direction);
				if (offset != Vector3I.Zero) dirVec = new Vector3(offset.X, offset.Y, offset.Z).Normalized();
				break;
		}

		return GetGridCellsInCone(startCell, dirVec, maxDistance, angles, includeOrigin);
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
					Vector3I gridCellCoords = startingGridCell.GridCoordinates + new Vector3I(x, y, z);
					GridCell gridCell = GetGridCell(gridCellCoords);
					if (gridCell == null)
						continue;

					if (onlyWalkable && !gridCell.IsWalkable) continue;

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

		int minZ = Mathf.FloorToInt(min.Z / _cellSize.X);
		int maxZ = Mathf.FloorToInt((max.Z - EPS) / _cellSize.X);

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
					Vector3 aabbMin = cell.WorldCenter - half;
					Vector3 aabbSize = new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X);
					var cellAabb = new Aabb(aabbMin, aabbSize);

					if (!areaAabb.Intersects(cellAabb))
						continue;

					// Sample at cell center
					pointParams.Position = cell.WorldCenter;

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

	public List<GridCell> GetCellsFromGridShape(GridPositionData positionData)
	{
		List<GridCell> occupiedCells = new List<GridCell>();

		if (positionData == null || positionData.Shape == null)
			return occupiedCells;

		GridCell rootCell = positionData.AnchorCell;
		if (rootCell == null)
			return occupiedCells;

		var worldCoords = positionData.Shape.GetWorldCoordinates(rootCell.GridCoordinates);

		foreach (var coord in worldCoords)
		{
			GridCell cell = GetGridCell(coord);
			if (cell != null)
				occupiedCells.Add(cell);
		}

		return occupiedCells;
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
		Vector3I targetCoords = startCell.GridCoordinates + offset;

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


	/// <summary>
	/// Creates and saves the editor configuration resource based on current settings.
	/// </summary>
	public void ExportEditorConfiguration()
	{
		var config = new GridConfiguration
		{
			CellSize = new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X),
			GridWorldOrigin = _gridWorldOrigin,
			GridSize = _gridSize
		};

		const string path = "res://Resources/GridConfiguration.tres";

		var dir = DirAccess.Open("res://");
		if (!dir.DirExists("Resources"))
		{
			dir.MakeDir("Resources");
		}

		var error = ResourceSaver.Save(config, path);
		if (error == Error.Ok)
		{
			GD.Print($"Grid configuration exported to {path}");
		}
		else
		{
			GD.PrintErr($"Failed to save grid configuration: {error}");
		}
	}

	#region manager Data

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		base.Load(data);
		if (!HasLoadedData) return;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		return new Godot.Collections.Dictionary<string, Variant>();
	}

	#endregion

	public override void Deinitialize()
	{
		return;
	}

	#endregion
}