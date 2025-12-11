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

  [Export] private int minHeightY =0;
  [Export] private int maxHeightY = 20;
  [Export]
  private Color color { get; set; }
  
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

  [ExportGroup("Man-made Blending")]
  [Export]
  private int blendRadiusCells { get; set; } = 6;

  // Shape of the blend curve (1.0 = smoothstep). >1 tightens near border,
  // <1 loosens near border.
  [Export]
  private float blendExponent { get; set; } = 1.0f;

  private ChunkData[] chunkTypes;
  private bool[,] lockedVertices;

  #endregion

  #region Setup and Execution

  public override string GetManagerName() =>"TerrainGenerator";

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
		  for (int chunkZ = 0; chunkZ < gameManager.mapSize.Y; chunkZ++)
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

	  // 3) Blend man-made borders smoothly to 0
	  BlendHeightsToZeroAroundManmade();

	  // 4) Smooth (ignore locked vertices)
	  ValidateHeightsIgnoringLocked(terrainHeights, 2, 2);

	  // 5) Clamp to a minimum Y level (procedural only by default).
	  // Define 'minHeightY' in your class (e.g., [Export] private float minHeightY)
	  ClampHeightsToMin(minHeightY, includeManMade: false);

	  // 6) Generate meshes for procedural chunks
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
    int vertsZ = (gameManager.mapSize.Y * chunkSize) + 1;

    terrainHeights = new Vector3[vertsX, vertsZ];
    lockedVertices = new bool[vertsX, vertsZ];

    FastNoiseLite noise = new FastNoiseLite
    {
      NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
      Seed = (int)GD.Randi(),
      Frequency = 0.1f, // Higher than default for more common features
    };
    noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
    noise.FractalOctaves = 3;
    noise.FractalLacunarity = 2.0f;
    noise.FractalGain = 0.5f;

    float chunkWorldSize = chunkSize * cellSize.X;
	
    for (int x = 0; x < vertsX; x++)
    {
      for (int z = 0; z < vertsZ; z++)
      {
        float worldX = x * cellSize.X;
        float worldZ = -(z * cellSize.X); // negative Z is forward (North)
        
        int chunkX = Mathf.Clamp(
          Mathf.FloorToInt(worldX / chunkWorldSize),
          0,
          gameManager.mapSize.X - 1
        );
        int chunkZ = Mathf.Clamp(
          Mathf.FloorToInt((-worldZ) / chunkWorldSize), // convert world Z back to grid Z
          0,
          gameManager.mapSize.Y - 1
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
          float rawNoise = noise.GetNoise2D(x, z); // Range [-1, 1]
          
          if (rawNoise < 0.0f) rawNoise = 0.0f;
          // Scale to desired max height (e.g., 30 units)
          float scaledNoise = rawNoise * maxHeightY;

          // Snap to grid defined by cellSize.Y
          y = Mathf.Round(scaledNoise / cellSize.Y) * cellSize.Y;
        }

        terrainHeights[x, z] = new Vector3(worldX, y, worldZ);
      }
    }
  }

  private void ClampHeightsToMin(float minY, bool includeManMade = false)
  {
	  if (terrainHeights == null)
		  return;

	  int vertsX = terrainHeights.GetLength(0);
	  int vertsZ = terrainHeights.GetLength(1);

	  // Keep grid quantization consistent; clamp to the next grid step at or above
	  // the requested minY.
	  float step = cellSize.Y;
	  float minYQuantized = step > 0f ? Mathf.Ceil(minY / step) * step : minY;

	  for (int z = 0; z < vertsZ; z++)
	  {
		  for (int x = 0; x < vertsX; x++)
		  {
			  if (
				  !includeManMade &&
				  lockedVertices != null &&
				  lockedVertices[x, z]
			  )
				  continue;

			  Vector3 v = terrainHeights[x, z];
			  if (v.Y < minYQuantized)
			  {
				  v.Y = minYQuantized;
				  terrainHeights[x, z] = v;
			  }
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

  public void ValidateHeightsIgnoringLocked(
    Vector3[,] verts,
    int validationPasses,
    float maxDifference
  )
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

  #region Man-made Blending

  private static float Smooth01(float t)
  {
    t = Mathf.Clamp(t, 0f, 1f);
    return t * t * (3f - 2f * t);
  }

  // Smoothly blends procedural terrain to 0 around any man-made chunk within
  // 'blendRadiusCells' (in vertex/cell units). This ensures crossable seams
  // and removes harsh borders.
  private void BlendHeightsToZeroAroundManmade()
  {
    if (terrainHeights == null || chunkTypes == null || chunkTypes.Length == 0)
      return;

    GameManager gameManager = GameManager.Instance;

    int vertsX = terrainHeights.GetLength(0);
    int vertsZ = terrainHeights.GetLength(1);

    // Pre-initialize weights to 1.0 (no change)
    float[,] weights = new float[vertsX, vertsZ];
    for (int z = 0; z < vertsZ; z++)
    {
      for (int x = 0; x < vertsX; x++)
        weights[x, z] = 1f;
    }

    int radius = Mathf.Max(1, blendRadiusCells);

    // Collect all man-made chunk rectangles in vertex index space
    var manmadeChunks = new List<(int vx0, int vz0, int vx1, int vz1)>();
    foreach (var c in chunkTypes)
    {
      if (c == null || c.chunkType != ChunkData.ChunkType.ManMade)
        continue;

      int cx = c.chunkCoordinates.X;
      int cz = c.chunkCoordinates.Y;

      int vx0 = cx * chunkSize;
      int vz0 = cz * chunkSize;
      int vx1 = vx0 + chunkSize;
      int vz1 = vz0 + chunkSize;

      manmadeChunks.Add((vx0, vz0, vx1, vz1));
    }

    if (manmadeChunks.Count == 0)
      return;

    // For each man-made chunk, compute a smooth falloff to 0 for nearby vertices
    foreach (var rect in manmadeChunks)
    {
      int vx0 = rect.vx0;
      int vz0 = rect.vz0;
      int vx1 = rect.vx1;
      int vz1 = rect.vz1;

      // Extended bounds to limit iteration
      int ex0 = Mathf.Clamp(vx0 - radius, 0, vertsX - 1);
      int ez0 = Mathf.Clamp(vz0 - radius, 0, vertsZ - 1);
      int ex1 = Mathf.Clamp(vx1 + radius, 0, vertsX - 1);
      int ez1 = Mathf.Clamp(vz1 + radius, 0, vertsZ - 1);

      for (int z = ez0; z <= ez1; z++)
      {
        for (int x = ex0; x <= ex1; x++)
        {
          // Distance in vertex-space to the rectangle (0 = on or inside)
          int dx =
            (x < vx0) ? (vx0 - x) : (x > vx1) ? (x - vx1) : 0;
          int dz =
            (z < vz0) ? (vz0 - z) : (z > vz1) ? (z - vz1) : 0;

          float dist = Mathf.Sqrt(dx * dx + dz * dz);

          if (dist > radius)
            continue;

          // Map distance to [0..1], apply optional exponent, then smoothstep
          float t = dist / radius;
          if (!Mathf.IsEqualApprox(blendExponent, 1.0f))
            t = Mathf.Pow(t, Mathf.Max(0.0001f, blendExponent));
          float factor = Smooth01(t);

          // Combine influences from multiple man-made chunks by taking the min
          if (factor < weights[x, z])
            weights[x, z] = factor;
        }
      }
    }

    // Apply weights to heights and lock exact border vertices (weight ~ 0)
    for (int z = 0; z < vertsZ; z++)
    {
      for (int x = 0; x < vertsX; x++)
      {
        float w = weights[x, z];

        // If already locked (inside man-made), keep it at 0
        if (lockedVertices[x, z])
        {
          // Ensure interior is exactly zero
          var v0 = terrainHeights[x, z];
          if (!Mathf.IsZeroApprox(v0.Y))
          {
            v0.Y = 0f;
            terrainHeights[x, z] = v0;
          }
          continue;
        }

        if (w < 1f)
        {
          var v = terrainHeights[x, z];
          float newY = v.Y * w;

          // Keep height quantized to the grid of cellSize.Y
          newY = Mathf.Round(newY / cellSize.Y) * cellSize.Y;
          v.Y = newY;
          terrainHeights[x, z] = v;

          // If weight is effectively 0, lock this vertex to keep the seam firm
          if (w <= 0.0001f)
            lockedVertices[x, z] = true;
        }
      }
    }

    GD.Print(
      $"MeshTerrainGenerator: Applied zero-blend radius {radius} cells " +
      $"around {manmadeChunks.Count} man-made chunk(s)."
    );
  }

  #endregion

  #region Man-made Border Baking (Legacy - not used)

  // Kept for reference; blending replaces the need for edge raycast baking.
  private void LockManmadeEdges()
  {
    GameManager gameManager = GameManager.Instance;
    int totalVertsX = gameManager.mapSize.X * chunkSize + 1;
    int totalVertsZ = gameManager.mapSize.Y * chunkSize + 1;

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
          ChunkData leftChunk = GetChunkFromVertexIndex(leftX, z, gameManager);

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
          ChunkData belowChunk =
            GetChunkFromVertexIndex(x, belowZ, gameManager);

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

  private bool TryRaycastManmadeHeightAtVertex(
    int vertexX,
    int vertexZ,
    out float height
  )
  {
    float worldX = vertexX * cellSize.X;
    float worldZ = -(vertexZ * cellSize.X); // negative Z is forward
    return TryRaycastManmadeHeightAtWorld(worldX, worldZ, out height);
  }

  private bool TryRaycastManmadeHeightAtWorld(
    float worldX,
    float worldZ,
    out float height
  )
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

  public ChunkData GetChunkFromVertexIndex(
    int vertexX,
    int vertexZ,
    GameManager gameManager
  )
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
    if (chunkZ < 0 || chunkZ >= gameManager.mapSize.Y)
      return null;
    return GetChunkData(chunkX, chunkZ);
  }

  public bool IsManMadeChunkAtWorld(
    float worldX,
    float worldZ,
    GameManager gameManager
  )
  {
    if (chunkTypes == null)
      return false;

    float chunkWorldSize = chunkSize * cellSize.X;
    int chunkX = Mathf.FloorToInt(worldX / chunkWorldSize);
    int chunkZ = Mathf.FloorToInt((-worldZ) / chunkWorldSize);

    if (
      chunkX < 0
      || chunkZ < 0
      || chunkX >= gameManager.mapSize.X
      || chunkZ >= gameManager.mapSize.Y
    )
      return false;

    return GetChunkData(chunkX, chunkZ).chunkType
      == ChunkData.ChunkType.ManMade;
  }

  public float SampleHeightAtCellCenter(
    int cellX,
    int cellZ,
    GameManager gameManager
  )
  {
    return SampleHeightAtCellCenterWithManmade(cellX, cellZ, gameManager);
  }

  public float SampleHeightAtCellCenterWithManmade(
    int cellX,
    int cellZ,
    GameManager gameManager
  )
  {
    float worldX = cellX * cellSize.X;
    float worldZ = -(cellZ * cellSize.X); // negative Z is forward
    return SampleHeightAtWorld(worldX, worldZ, gameManager);
  }

  public float SampleHeightAtWorld(
    float worldX,
    float worldZ,
    GameManager gameManager
  )
  {
    return SampleHeightAtWorldWithManmade(worldX, worldZ, gameManager);
  }

  public float SampleHeightAtWorldWithManmade(
    float worldX,
    float worldZ,
    GameManager gameManager
  )
  {
    if (IsManMadeChunkAtWorld(worldX, worldZ, gameManager))
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
      Mathf.RoundToInt((-worldZ) / cellSize.X),
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
      GameManager.Instance.mapSize.X * chunkSize, // X dimension (East-West)
      chunkSize, // Y dimension (height)
      GameManager.Instance.mapSize.Y * chunkSize // Z dimension (North-South)
    );

  public bool HasHeightData => terrainHeights != null;

  public ChunkData[] GetChunks() => chunkTypes;

  #region manager Data

  public override void Load(Godot.Collections.Dictionary<string,Variant> data)
  {
    GD.Print("No data to transfer");
  }

  public override Godot.Collections.Dictionary<string,Variant> Save()
  {
    return null;
  }

  #endregion
  #endregion
}