using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class MeshTerrainGenerator : Manager<MeshTerrainGenerator>
{
  #region Variables
  
  [Export]
  public int chunkSize { get; set; }

  [Export]
  public Vector2 cellSize { get; set; }

  [Export]
  private Color color { get; set; }

  // Height field: [X, Z] where X=East-West, Z=North-South
  public Vector3[,] terrainHeights { get; set; }

  [ExportGroup("Chunk Overrides")]
  [Export]
  private ChunkData[] chunkOverrides { get; set; } = Array.Empty<ChunkData>();

  [ExportGroup("Raycast Sampling")]
  [Export]
  private float manmadeRaycastHeight { get; set; } = 5000f;

  [Export]
  private float manmadeRaycastLength { get; set; } = 10000f;

  [Export]
  private uint manmadeRaycastMask { get; set; } = 0;

  private ChunkData[] chunkTypes;
  private bool[,] lockedVertices;

  #endregion

  #region Setup and Execution

  protected override async Task _Setup()
  {
    BuildChunkTypesFromOverrides();
    GenerateHeightMap();
    GD.Print("MeshTerrainGenerator: base height data ready.");
    await Task.CompletedTask;
  }

  protected override async Task _Execute()
  {
	  GameManager gameManager = GameManager.Instance;
    // 1) Build/instantiate ALL chunk nodes
    for (int chunkX = 0; chunkX < gameManager.mapSize.X; chunkX++)
    {
      for (int chunkZ = 0; chunkZ < gameManager.mapSize.Y; chunkZ++) // mapSize.Y represents Z dimension
      {
        EnsureChunkNodeExists(chunkX, chunkZ);

        var cData = GetChunkData(chunkX, chunkZ);
        var node = cData.GetChunkNode();

        float chunkWorldSize = chunkSize * cellSize.X;
        node.Position = new Vector3(
          chunkX * chunkWorldSize,
          0f,
          -(chunkZ * chunkWorldSize) // negative Z is forward (North)
        );
      }
    }

    // 2) Wait one physics frame
    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

    // 3) Bake man-made border heights
    LockManmadeEdges();

    // 4) Smooth (ignore locked vertices)
    ValidateHeightsIgnoringLocked(terrainHeights, 2, 2);

    // 5) Generate meshes for procedural chunks
    for (int chunkX = 0; chunkX < gameManager.mapSize.X; chunkX++)
    {
      for (int chunkZ = 0; chunkZ < gameManager.mapSize.Y; chunkZ++)
      {
        var cData = GetChunkData(chunkX, chunkZ);
        if (cData.chunkType == ChunkData.ChunkType.ManMade)
          continue;

        cData.chunk.Initialize(
          chunkX,
          chunkZ,
          chunkSize,
          terrainHeights,
          cellSize.X,
          cData
        );
        cData.chunk.Generate(color);
      }
    }

    GD.Print("MeshTerrainGenerator: chunks built.");
    await Task.CompletedTask;
  }

  #endregion

  #region Heightmap Generation

  public void GenerateHeightMap()
  {
	  GameManager gameManager = GameManager.Instance;
    // CONSISTENT: [X, Z] where X=East-West vertices, Z=North-South vertices
    int vertsX = (gameManager.mapSize.X * chunkSize) + 1;
    int vertsZ = (gameManager.mapSize.Y * chunkSize) + 1; // mapSize.Y = chunks in Z direction

    terrainHeights = new Vector3[vertsX, vertsZ];
    lockedVertices = new bool[vertsX, vertsZ];

    FastNoiseLite noise = new FastNoiseLite
    {
      NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
      Seed = (int)GD.Randi(),
      Frequency = 1.0f / 10
    };
    noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
    noise.FractalOctaves = 4;
    noise.FractalLacunarity = 2.0f;
    noise.FractalGain = 0.5f;

    float chunkWorldSize = chunkSize * cellSize.X;

    // CONSISTENT: Loop over X (East-West) and Z (North-South)
    for (int x = 0; x < vertsX; x++)
    {
      for (int z = 0; z < vertsZ; z++)
      {
        // World coordinates
        float worldX = x * cellSize.X;
        float worldZ = -(z * cellSize.X); // negative Z is forward (North)

        // Determine which chunk this vertex belongs to
        int chunkX = Mathf.Clamp(
          Mathf.FloorToInt(worldX / chunkWorldSize),
          0,
          gameManager.mapSize.X - 1
        );
        int chunkZ = Mathf.Clamp(
          Mathf.FloorToInt((-worldZ) / chunkWorldSize), // convert world Z back to grid Z
          0,
          gameManager.mapSize.Y - 1 // mapSize.Y = number of chunks in Z direction
        );

        bool isManmade =
          chunkTypes != null
          && GetChunkData(chunkX, chunkZ).chunkType == ChunkData.ChunkType.ManMade;

        float y;
        if (isManmade)
        {
          y = 0f;
          lockedVertices[x, z] = true;
        }
        else
        {
          y = Mathf.Round(
            (noise.GetNoise2D(x, z) + 1.0f) / cellSize.Y
          ) * cellSize.Y;
        }

        terrainHeights[x, z] = new Vector3(worldX, y, worldZ);
      }
    }
  }

  private void BuildChunkTypesFromOverrides()
  {
	  GameManager gameManager = GameManager.Instance;
    int chunksX = gameManager.mapSize.X; // East-West
    int chunksZ = gameManager.mapSize.Y; // North-South (depth)

    if (chunksX <= 0 || chunksZ <= 0)
    {
      chunkTypes = Array.Empty<ChunkData>();
      GD.PrintErr(
        "MeshTerrainGenerator: mapSize is invalid; chunkTypes cleared."
      );
      return;
    }

    int count = chunksX * chunksZ;

    if (chunkTypes == null || chunkTypes.Length != count)
      chunkTypes = new ChunkData[count];

    // CONSISTENT: Loop uses chunkX and chunkZ naming
    for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++)
    {
      for (int chunkX = 0; chunkX < chunksX; chunkX++)
      {
        int idx = chunkX + chunkZ * chunksX;
        var defaultData = new ChunkData
        {
          chunkCoordinates = new Vector2I(chunkX, chunkZ),
          chunkType = ChunkData.ChunkType.Procedural
        };
        chunkTypes[idx] = defaultData;
      }
    }

    // Apply overrides
    if (chunkOverrides == null || chunkOverrides.Length == 0)
      return;

    var seen = new HashSet<Vector2I>();
    for (int i = 0; i < chunkOverrides.Length; i++)
    {
      var ov = chunkOverrides[i];
      if (ov == null)
        continue;

      Vector2I coords = ov.chunkCoordinates; // X and Y (representing X and Z)
      if (
        coords.X < 0
        || coords.Y < 0
        || coords.X >= chunksX
        || coords.Y >= chunksZ
      )
      {
        GD.PrintErr(
          $"Chunk override [{i}] has out-of-range coords {coords}. "
            + $"Valid range: X:[0..{chunksX - 1}] Z:[0..{chunksZ - 1}]. "
            + "Skipping."
        );
        continue;
      }

      int idx = coords.X + coords.Y * chunksX;

      ChunkData ovCopy = ov.Duplicate(true) as ChunkData;
      if (ovCopy == null)
      {
        ovCopy = new ChunkData
        {
          chunkCoordinates = ov.chunkCoordinates,
          chunkType = ov.chunkType
        };
      }

      ovCopy.SetChunkNode(null);
      ovCopy.chunk = null;

      chunkTypes[idx] = ovCopy;

      if (!seen.Add(coords))
        GD.Print($"Duplicate override for coords {coords}; last wins.");
    }
  }

  public void ValidateHeightsIgnoringLocked(Vector3[,] verts, int validationPasses, float maxDifference)
  {
    int vertsX = verts.GetLength(0);
    int vertsZ = verts.GetLength(1);
    
    for (int pass = 0; pass < validationPasses; pass++)
    {
      // CONSISTENT: x and z naming for vertices
      for (int z = 0; z < vertsZ - 1; z++)
      {
        for (int x = 0; x < vertsX - 1; x++)
        {
          if (lockedVertices[x, z])
            continue;

          float[] cellHeights =
          {
            verts[x, z].Y,
            verts[x + 1, z].Y,
            verts[x, z + 1].Y,
            verts[x + 1, z + 1].Y
          };

          int uniqueHeights = cellHeights.Distinct().Count();

          if (uniqueHeights > 2)
          {
            var grouped = cellHeights
              .GroupBy(h => h)
              .OrderByDescending(g => g.Count())
              .ToList();

            float heightToReplace = grouped.Last().Key;
            float replacementHeight = grouped[0].Key;

            for (int i2 = 0; i2 < 4; i2++)
            {
              if (cellHeights[i2] == heightToReplace)
              {
                switch (i2)
                {
                  case 0:
                    verts[x, z].Y = replacementHeight;
                    break;
                  case 1:
                    verts[x + 1, z].Y = replacementHeight;
                    break;
                  case 2:
                    verts[x, z + 1].Y = replacementHeight;
                    break;
                  case 3:
                    verts[x + 1, z + 1].Y = replacementHeight;
                    break;
                }
              }
            }
          }
          else if (
            uniqueHeights == 2
            && verts[x, z].Y == verts[x + 1, z + 1].Y
            && verts[x + 1, z].Y == verts[x, z + 1].Y
          )
          {
            verts[x + 1, z + 1] = new Vector3(
              verts[x + 1, z + 1].X,
              verts[x, z].Y,
              verts[x + 1, z + 1].Z
            );
          }
        }
      }
    }
  }

  #endregion

  #region Chunk Node Management

  private void EnsureChunkNodeExists(int chunkX, int chunkZ)
  {
    ChunkData cData = GetChunkData(chunkX, chunkZ);
    if (cData == null)
    {
      GD.PrintErr($"Null chunk data at {chunkX}, {chunkZ}");
      return;
    }

    Node3D chunkNode = cData.GetChunkNode();
    if (chunkNode == null)
    {
      if (cData.chunkType == ChunkData.ChunkType.ManMade)
      {
        string prefabPath = GetChunkPrefabPath(cData.GetchunkGOIndex());
        GD.Print($"Loading ManMade chunk from: {prefabPath}");

        if (!ResourceLoader.Exists(prefabPath))
        {
          GD.PrintErr($"Chunk prefab not found: {prefabPath}");
          cData.chunkType = ChunkData.ChunkType.Procedural;

          chunkNode = new Node3D { Name = $"Chunk_{chunkX}_{chunkZ}" };
          AddChild(chunkNode, forceReadableName: true);
        }
        else
        {
          PackedScene chunkScene =
            ResourceLoader.Load<PackedScene>(prefabPath);
          if (chunkScene == null)
          {
            GD.PrintErr(
              $"Failed to load PackedScene at {prefabPath}, "
                + "falling back to Procedural."
            );
            cData.chunkType = ChunkData.ChunkType.Procedural;

            chunkNode = new Node3D { Name = $"Chunk_{chunkX}_{chunkZ}" };
            AddChild(chunkNode, forceReadableName: true);
          }
          else
          {
            chunkNode = chunkScene.Instantiate<Node3D>();
            chunkNode.Name = $"Chunk_{chunkX}_{chunkZ}";
            AddChild(chunkNode, forceReadableName: true);
          }
        }
      }
      else
      {
        chunkNode = new Node3D { Name = $"Chunk_{chunkX}_{chunkZ}" };
        AddChild(chunkNode, forceReadableName: true);
      }

      cData.SetChunkNode(chunkNode);
      EnsureChunkComponent(cData);
    }
    else
    {
      EnsureChunkComponent(cData);
    }
  }

  private void EnsureChunkComponent(ChunkData cData)
  {
    var node = cData.GetChunkNode();
    if (node == null)
      return;

    if (node is Chunk rootChunk)
    {
      cData.chunk = rootChunk;
      return;
    }

    var comp = node.GetOrCreateChildOfType<Chunk>();
    if (comp == null)
    {
      comp = new Chunk();
      node.AddChild(comp);
    }
    cData.chunk = comp;
  }

  #endregion

  #region Man-made Border Baking

  private void LockManmadeEdges()
  {
	  GameManager gameManager = GameManager.Instance;
    int totalVertsX = gameManager.mapSize.X * chunkSize + 1;
    int totalVertsZ = gameManager.mapSize.Y * chunkSize + 1;

    // CONSISTENT: Use x and z for vertex indices
    for (int z = 0; z < totalVertsZ; z++)
    {
      for (int x = 0; x < totalVertsX; x++)
      {
        bool isVerticalBoundary = (x % chunkSize == 0 && x != 0);
        bool isHorizontalBoundary = (z % chunkSize == 0 && z != 0);

        if (!isVerticalBoundary && !isHorizontalBoundary)
          continue;

        ChunkData currentChunk = GetChunkFromVertexIndex(x, z, gameManager);
        if (currentChunk == null)
          continue;

        // Check vertical boundary: between left and right chunks
        if (isVerticalBoundary)
        {
          int leftX = x - 1;
          ChunkData leftChunk = GetChunkFromVertexIndex(leftX, z,gameManager);

          if (leftChunk != null)
          {
            bool boundaryBetweenManmadeAndProc =
              (currentChunk.chunkType == ChunkData.ChunkType.ManMade
                && leftChunk.chunkType == ChunkData.ChunkType.Procedural)
              || (currentChunk.chunkType == ChunkData.ChunkType.Procedural
                && leftChunk.chunkType == ChunkData.ChunkType.ManMade);

            if (boundaryBetweenManmadeAndProc)
            {
              if (TryRaycastManmadeHeightAtVertex(x, z, out float height))
              {
                Vector3 v = terrainHeights[x, z];
                v.Y = height;
                terrainHeights[x, z] = v;
                lockedVertices[x, z] = true;
              }
            }
          }
        }

        // Check horizontal boundary: between adjacent chunks in Z direction
        if (isHorizontalBoundary)
        {
          int belowZ = z - 1;
          ChunkData belowChunk = GetChunkFromVertexIndex(x, belowZ, gameManager);

          if (belowChunk != null)
          {
            bool boundaryBetweenManmadeAndProc =
              (currentChunk.chunkType == ChunkData.ChunkType.ManMade
                && belowChunk.chunkType == ChunkData.ChunkType.Procedural)
              || (currentChunk.chunkType == ChunkData.ChunkType.Procedural
                && belowChunk.chunkType == ChunkData.ChunkType.ManMade);

            if (boundaryBetweenManmadeAndProc)
            {
              if (TryRaycastManmadeHeightAtVertex(x, z, out float height))
              {
                Vector3 v = terrainHeights[x, z];
                v.Y = height;
                terrainHeights[x, z] = v;
                lockedVertices[x, z] = true;
              }
            }
          }
        }
      }
    }
  }

  private bool TryRaycastManmadeHeightAtVertex(int vertexX, int vertexZ, out float height)
  {
    float worldX = vertexX * cellSize.X;
    float worldZ = -(vertexZ * cellSize.X); // negative Z is forward
    return TryRaycastManmadeHeightAtWorld(worldX, worldZ, out height);
  }

  private bool TryRaycastManmadeHeightAtWorld(float worldX, float worldZ, out float height)
  {
    height = 0f;

    var spaceState = GetTree().Root.GetWorld3D().DirectSpaceState;

    Vector3 from = new Vector3(worldX, manmadeRaycastHeight, worldZ);
    Vector3 to = from + Vector3.Down * manmadeRaycastLength;

    var query = PhysicsRayQueryParameters3D.Create(from, to);
    query.CollideWithAreas = true;
    if (manmadeRaycastMask != 0)
      query.CollisionMask = manmadeRaycastMask;

    var result = spaceState.IntersectRay(query);
    if (result != null && result.Count > 0)
    {
      height = result["position"].As<Vector3>().Y;
      return true;
    }
    return false;
  }

  #endregion

  #region Sampling helpers

  public ChunkData GetChunkFromVertexIndex(int vertexX, int vertexZ, GameManager gameManager)
  {
    // Map vertex indices to chunk coordinates
    if (vertexX < 0)
      vertexX = 0;
    else if (vertexX >= gameManager.mapSize.X * chunkSize)
      vertexX = gameManager.mapSize.X * chunkSize - 1;

    if (vertexZ < 0)
      vertexZ = 0;
    else if (vertexZ >= gameManager.mapSize.Y * chunkSize)
      vertexZ = gameManager.mapSize.Y * chunkSize - 1;

    int chunkX = vertexX / chunkSize;
    int chunkZ = vertexZ / chunkSize;

    if (chunkX < 0 || chunkX >= gameManager.mapSize.X)
      return null;
    if (chunkZ < 0 || chunkZ >= gameManager.mapSize.Y) // mapSize.Y = chunks in Z direction
      return null;
    return GetChunkData(chunkX, chunkZ);
  }

  public bool IsManMadeChunkAtWorld(float worldX, float worldZ, GameManager gameManager)
  {
    if (chunkTypes == null)
      return false;

    float chunkWorldSize = chunkSize * cellSize.X;
    int chunkX = Mathf.FloorToInt(worldX / chunkWorldSize);
    int chunkZ = Mathf.FloorToInt((-worldZ) / chunkWorldSize); // convert world Z to grid Z

    if (chunkX < 0 || chunkZ < 0 || chunkX >= gameManager.mapSize.X || chunkZ >= gameManager.mapSize.Y)
      return false;

    return GetChunkData(chunkX, chunkZ).chunkType == ChunkData.ChunkType.ManMade;
  }

  public float SampleHeightAtCellCenter(int cellX, int cellZ,GameManager gameManager)
  {
    return SampleHeightAtCellCenterWithManmade(cellX, cellZ,gameManager);
  }

  public float SampleHeightAtCellCenterWithManmade(int cellX, int cellZ,GameManager gameManager)
  {
    float worldX = cellX * cellSize.X;
    float worldZ = -(cellZ * cellSize.X); // negative Z is forward
    return SampleHeightAtWorld(worldX, worldZ,gameManager);
  }

  public float SampleHeightAtWorld(float worldX, float worldZ, GameManager gameManager)
  {
    return SampleHeightAtWorldWithManmade(worldX, worldZ,gameManager);
  }

  public float SampleHeightAtWorldWithManmade(float worldX, float worldZ, GameManager gameManager)
  {
    if (IsManMadeChunkAtWorld(worldX, worldZ,gameManager))
    {
      if (TryRaycastManmadeHeightAtWorld(worldX, worldZ, out float h))
        return h;

      return SampleHeightFromHeightmap(worldX, worldZ);
    }

    return SampleHeightFromHeightmap(worldX, worldZ);
  }

  private float SampleHeightFromHeightmap(float worldX, float worldZ)
  {
    if (terrainHeights == null)
      throw new InvalidOperationException(
        "SampleHeightFromHeightmap called before terrain heights are initialized."
      );

    int ix = Mathf.Clamp(
      Mathf.RoundToInt(worldX / cellSize.X),
      0,
      terrainHeights.GetLength(0) - 1
    );
    int iz = Mathf.Clamp(
      Mathf.RoundToInt((-worldZ) / cellSize.X), // convert world Z to grid Z
      0,
      terrainHeights.GetLength(1) - 1
    );
    return terrainHeights[ix, iz].Y;
  }

  #endregion

  #region Get/Set Functions

  public ChunkData GetChunkData(int chunkX, int chunkZ) =>
    chunkTypes[chunkX + chunkZ * GameManager.Instance.mapSize.X];

  private string GetChunkPrefabPath(string chunkId)
  {
    return $"res://Scenes/Chunks/{chunkId}.tscn";
  }

  public Vector2I GetMapSize() => GameManager.Instance.mapSize;
  public int GetChunkSize() => chunkSize;
  public Vector2 GetCellSize() => cellSize;

  public Vector3I GetMapCellSize() =>
    new Vector3I(
	    GameManager.Instance.mapSize.X * chunkSize,      // X dimension (East-West)
      chunkSize,                   // Y dimension (height)
      GameManager.Instance.mapSize.Y * chunkSize        // Z dimension (North-South)
    );

  public bool HasHeightData => terrainHeights != null;

  public ChunkData[] GetChunks() => chunkTypes;
  
  
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