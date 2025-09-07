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

  private const float EPS = 0.0001f;

  [Export] private Vector3I _gridSize;
  [Export] private Vector2 _cellSize;

  public GridCell[][,] GridCells { get; private set; }

  [ExportCategory("Raycasting"), Export]
  private bool _raycastCheck;

  [Export] private float _raycastDistance; // kept for compatibility
  [Export] private Vector3 _raycastOffset;

  [ExportCategory("Collision Testing"), Export]
  private bool _collisionCheck;

  [Export] private Vector3 _collisionSize;
  [Export] private Vector3 _collisionOffset;

  #endregion

  #region Signals

  [Signal]
  public delegate void GridSystemInitializedEventHandler();

  #endregion

  #region Functions

  public override void _PhysicsProcess(double delta)
  {
    if (!ExecuteComplete || GridCells == null)
      return;

    for (int y = 0; y < _gridSize.Y; y++)
    {
      for (int x = 0; x < _gridSize.X; x++)
      {
        for (int z = 0; z < _gridSize.Z; z++)
        {
          var gridCell = GridCells[y][x, z];
          if (gridCell == null)
            continue;

          if (!gridCell.state.HasFlag(Enums.GridCellState.Air))
            VisualizeCell(gridCell);
        }
      }
    }
  }

  #region Manager Functions

  protected override async Task _Setup()
  {
    var gen = MeshTerrainGenerator.Instance;
    _cellSize = gen.GetCellSize();
    _gridSize = gen.GetMapCellSize();
    await Task.CompletedTask;
  }

  protected override async Task _Execute()
  {
    await SetupGrid();
    await SetupCellConnections();

    EmitSignal(SignalName.GridSystemInitialized);
  }

  #endregion

  private async Task SetupGrid()
  {
    GridCells = new GridCell[_gridSize.Y][,];
    var space = GetTree().Root.GetWorld3D().DirectSpaceState;

    // Obstruction (non-terrain) shape overlap parameters.
    PhysicsShapeQueryParameters3D obstacleParams = null;
    if (_collisionCheck)
    {
      var boxShape = new BoxShape3D { Size = _collisionSize };
      obstacleParams = new PhysicsShapeQueryParameters3D
      {
        Shape = boxShape,
        CollideWithBodies = true,
        CollideWithAreas = false,
        CollisionMask = ~(uint)PhysicsLayer.TERRAIN
      };
    }

    // Terrain raycast mask.
    uint groundMask = (uint)PhysicsLayer.TERRAIN;

    float halfY = _cellSize.Y * 0.5f;
    // Small bias so exact boundary surfaces are attributed to the lower cell.
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

          // worldCenter is TOP center of the cell
          Vector3 worldCenter = new Vector3(
            (x + 0.5f) * _cellSize.X,
            (y + 1.0f) * _cellSize.Y,
            (z + 0.5f) * _cellSize.X
          );

          bool hasGround = true; // default when ground checking disabled
          bool obstructed = false;

          if (_raycastCheck)
          {
            // Constrain the ray to this cell's vertical span (top-inclusive, bottom-exclusive):
            // from = just above the top face; to = just above the bottom face.
            // Since worldCenter is the top center, bottom is worldCenter + Down * _cellSize.Y.
            Vector3 from =
              worldCenter + _raycastOffset + Vector3.Up * surfEps;
            Vector3 to =
              worldCenter
              + _raycastOffset
              + Vector3.Down * (_cellSize.Y - surfEps);

            var rayParams = new PhysicsRayQueryParameters3D
            {
              From = from,
              To = to,
              CollideWithBodies = true,
              CollideWithAreas = false,
              CollisionMask = groundMask
            };

            var hit = space.IntersectRay(rayParams);
            hasGround = hit.Count > 0;
          }

          if (_collisionCheck)
          {
            // Center the obstruction shape within the cell volume
            var centerOfCell = worldCenter + Vector3.Down * halfY;
            obstacleParams.Transform = new Transform3D(
              Basis.Identity,
              centerOfCell + _collisionOffset
            );

            var overlaps = space.IntersectShape(obstacleParams, 8);
            obstructed = overlaps != null && overlaps.Count > 0;
          }

          // Compose final flags.
          Enums.GridCellState state = Enums.GridCellState.None;

          if (_raycastCheck)
          {
            state |= hasGround
              ? Enums.GridCellState.Ground
              : Enums.GridCellState.Air;
          }

          if (_collisionCheck && obstructed)
            state |= Enums.GridCellState.Obstructed;

          bool walkable = true;
          if (_raycastCheck && !hasGround)
            walkable = false;
          if (_collisionCheck && obstructed)
            walkable = false;

          state |= walkable
            ? Enums.GridCellState.Walkable
            : Enums.GridCellState.Unwalkable;

          CreateOrUpdateCell(coords, worldCenter, state, inventoryManager);
        }
      }
    }

    await Task.CompletedTask;
  }

  private async Task SetupCellConnections()
  {
    for (int y = 0; y < _gridSize.Y; y++)
    {
      for (int x = 0; x < _gridSize.X; x++)
      {
        for (int z = 0; z < _gridSize.Z; z++)
        {
          Vector3I coords = new Vector3I(x, y, z);
          GridCell gridCell = GridCells[y][x, z];
          if (gridCell == null)
            continue;

          if (!gridCell.state.HasFlag(Enums.GridCellState.Walkable))
            continue;

          List<CellConnection> connections = new List<CellConnection>();
          for (int relY = -1; relY <= 1; relY++)
          {
            for (int relX = -1; relX <= 1; relX++)
            {
              for (int relZ = -1; relZ <= 1; relZ++)
              {
                if (relX == 0 && relY == 0 && relZ == 0)
                  continue;

                Vector3I neighborCoords =
                  coords + new Vector3I(relX, relY, relZ);
                GridCell neighbor = GetGridCell(neighborCoords);
                if (neighbor == null)
                  continue;

                if (neighbor.state.HasFlag(Enums.GridCellState.Walkable))
                {
                  connections.Add(
                    new CellConnection(
                      gridCell.gridCoordinates,
                      neighbor.gridCoordinates
                    )
                  );
                }
              }
            }
          }

          gridCell.SetConnections(connections);
        }
      }
    }

    await Task.CompletedTask;
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
    float ny = worldPosition.Y / _cellSize.Y;
    int Y = Mathf.FloorToInt(ny - EPS);
    int Z = Mathf.FloorToInt(worldPosition.Z / _cellSize.X);

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
    Enums.GridCellState stateFilter,
    out GridCell gridCell,
    bool onlyNonOccupied = true,
    Enums.UnitTeam teamFilter = Enums.UnitTeam.None
  )
  {
    gridCell = null;
    List<GridCell> cells = new List<GridCell>();

    for (int y = 0; y < GridCells.Length; y++)
    {
      for (int x = 0; x < GridCells[y].GetLength(0); x++)
      {
        for (int z = 0; z < GridCells[y].GetLength(1); z++)
        {
          GridCell candidate = GridCells[y][x, z];
          if (candidate == null)
            continue;

          if (stateFilter != Enums.GridCellState.None)
          {
            if (!candidate.state.HasFlag(stateFilter))
              continue;
          }

          if (onlyNonOccupied && candidate.HasGridObject())
            continue;

          if (
            teamFilter != Enums.UnitTeam.None
            && candidate.UnitTeamSpawn.HasFlag(teamFilter)
          )
            continue;

          cells.Add(candidate);
        }
      }
    }

    if (cells == null || cells.Count < 1)
      return false;

    int randomIndex = GD.RandRange(0, cells.Count - 1);
    gridCell = cells[randomIndex];
    return true;
  }

  public void VisualizeCell(GridCell gridCell, bool hideAir = true, int duration = -1)
  {
    var s = gridCell.state;

    // Skip if it's purely air.
    if (hideAir && s.HasFlag(Enums.GridCellState.Air)
      && !s.HasFlag(Enums.GridCellState.Ground)
    )
      return;

    Color color = new Color(1f, 1f, 1f, 1f);

    if (s.HasFlag(Enums.GridCellState.Obstructed))
    {
      color = new Color(1f, 0f, 0f, 1f); // blocked
    }
    else if (s.HasFlag(Enums.GridCellState.Walkable))
    {
      color = s.HasFlag(Enums.GridCellState.Ground)
        ? new Color(0f, 1f, 0f, 1f)
        : new Color(0.5f, 1f, 0.5f, 1f);
    }
    else if (s.HasFlag(Enums.GridCellState.Ground))
    {
      color = new Color(1f, 1f, 0f, 1f);
    }

    // Draw connections from the top-center
    foreach (CellConnection connection in gridCell.connections)
    {
      GridCell to = GetGridCell(connection.ToGridCellCoords);
      if (to == null)
        continue;

      DebugDraw3D.DrawLine(gridCell.worldCenter, to.worldCenter, color);
    }

    // Draw the cell volume as a box centered in the volume (not at the top).
    Vector3 drawCenter = gridCell.worldCenter + Vector3.Down * (_cellSize.Y * 0.5f);
    DebugDraw3D.DrawBox(
      drawCenter,
      Quaternion.Identity,
      new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X),
      color,
      true,
      duration != -1 ? duration : 0
    );
  }

  public bool TryGetGridCellNeighbors(
    GridCell startingGridCell,
    out List<GridCell> neighbors
  )
  {
    neighbors = new List<GridCell>();
    if (startingGridCell == null)
      return false;

    foreach (var connection in startingGridCell.connections)
    {
      var coords = connection.ToGridCellCoords;
      var n = GetGridCell(coords);
      if (n != null)
        neighbors.Add(n);
    }

    return neighbors.Count > 0;
  }


  public bool TryGetCellsInRange(GridCell startingGridCell, Vector2I range, out List<GridCell> neighbors, Enums.GridCellState stateFilter = Enums.GridCellState.None)
  {
	  neighbors = new List<GridCell>();
	  if (startingGridCell == null)
        return false;
	  if(range.X < 1 || range.Y < 1) return false;

	  for (int y = Mathf.RoundToInt(-range.Y / 2.0); y < Mathf.RoundToInt(range.Y / 2.0); y++)
	  {
		  for (int x = Mathf.RoundToInt(-range.X / 2.0); x < Mathf.RoundToInt(range.X / 2.0); x++)
		  {
			  for (int z = Mathf.RoundToInt(-range.X / 2.0); z < Mathf.RoundToInt(range.X / 2.0); z++)
			  {
				  Vector3I gridCellCoords = new Vector3I(x, y, z);
				  GridCell gridCell = GetGridCell(gridCellCoords);
				  if (gridCell == null) continue;
				  
				  if(stateFilter != Enums.GridCellState.None && !gridCell.state.HasFlag(stateFilter)) continue;
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

    // With top-centered worldCenter, compute the cell AABB from top center.
    Vector3 half = new Vector3(
      _cellSize.X * 0.5f,
      _cellSize.Y * 0.5f,
      _cellSize.X * 0.5f
    );

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

          // AABB from top center
          Vector3 aabbMin = cell.worldCenter - new Vector3(half.X, _cellSize.Y, half.Z);
          Vector3 aabbSize = new Vector3(_cellSize.X, _cellSize.Y, _cellSize.X);
          var cellAabb = new Aabb(aabbMin, aabbSize);

          if (!areaAabb.Intersects(cellAabb))
            continue;

          // Sample at cell volume center to avoid boundary ambiguity
          pointParams.Position = cell.worldCenter + Vector3.Down * (_cellSize.Y * 0.5f);

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

  // Builds a world-space AABB that contains all CollisionShape3D children of the Area3D.
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

      // Local-space AABB per shape type.
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
          float h = cap.Height; // cylinder part only; hemispheres add r each
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
          // Fallback: use debug mesh AABB if available.
          var dbg = shape.GetDebugMesh();
          if (dbg == null)
            continue;
          local = dbg.GetAabb();
          break;
        }
      }

      // Transform to world and merge.
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

  // Transforms a local AABB by an arbitrary Transform3D and returns the
  // enclosing axis-aligned AABB in world space.
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

    // 8 corners of the AABB
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

  #endregion
}