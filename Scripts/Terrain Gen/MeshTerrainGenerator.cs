using System;
using System.Collections.Generic;
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
    public Vector2I mapSize { get; set; }

    [Export]
    public int chunkSize { get; set; }

    [Export]
    public Vector2 cellSize { get; set; }

    [Export]
    private Color color { get; set; }

    // Height field built at runtime: size = (mapSize * chunkSize) + 1
    public Vector3[,] terrainHeights { get; set; }

    // Inspector-exposed list of overrides for specific chunks.
    [ExportGroup("Chunk Overrides")]
    [Export]
    private ChunkData[] chunkOverrides { get; set; } = Array.Empty<ChunkData>();

    // Raycast sampling settings for man-made chunks
    [ExportGroup("Raycast Sampling")]
    [Export]
    private float manmadeRaycastHeight { get; set; } = 5000f;

    [Export]
    private float manmadeRaycastLength { get; set; } = 10000f;

    // Optional: set a specific collision mask. 0 = use default.
    [Export]
    private uint manmadeRaycastMask { get; set; } = 0;

    // Runtime-built full grid of ChunkData, sized to map
    private ChunkData[] chunkTypes;

    private bool[,] lockedVertices;

    #endregion

    #region Setup and Execution

    protected override async Task _Setup()
    {
        // Build chunk types first so height map can respect man-made regions
        BuildChunkTypesFromOverrides();

        GenerateHeightMap();

        GD.Print("MeshTerrainGenerator: base height data ready.");
        await Task.CompletedTask;
    }

    protected override async Task _Execute()
    {
	    // 1) Build/instantiate ALL chunk nodes (man-made prefabs now) and place them
	    for (int x = 0; x < mapSize.X; x++)
	    {
		    for (int z = 0; z < mapSize.Y; z++)
		    {
			    EnsureChunkNodeExists(x, z);

			    // Anchor each chunk at its top-left heightmap vertex (corner)
			    var cData = GetChunkData(x, z);
			    var node = cData.GetChunkNode();

			    node.Position = new Vector3(
				    x * chunkSize * cellSize.X,
				    0f,
				    z * chunkSize * cellSize.X
			    );
		    }
	    }

	    // 2) Wait one physics frame so man-made colliders are registered
	    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

	    // 3) Bake man-made border heights into the shared heightmap and lock them
	    LockManmadeEdges();

	    // 4) Smooth (but ignore locked vertices)
	    ValidateHeightsIgnoringLocked(terrainHeights, 2, 2);

	    // 5) Initialize & generate meshes for procedural chunks
	    for (int x = 0; x < mapSize.X; x++)
	    {
		    for (int z = 0; z < mapSize.Y; z++)
		    {
			    var cData = GetChunkData(x, z);
			    if (cData.chunkType == ChunkData.ChunkType.ManMade)
				    continue; // Prefabs handle their own visuals

			    cData.chunk.Initialize(
				    x,
				    z,
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
        // Grid is cells + 1 in each dimension
        int vertsX = (mapSize.X * chunkSize) + 1;
        int vertsZ = (mapSize.Y * chunkSize) + 1;

        terrainHeights = new Vector3[vertsX, vertsZ];
        lockedVertices = new bool[vertsX, vertsZ];

        // Noise for procedural areas only
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

        for (int x = 0; x < vertsX; x++)
        {
            for (int z = 0; z < vertsZ; z++)
            {
                float worldX = x * cellSize.X;
                float worldZ = z * cellSize.X;

                int cx = Mathf.Clamp(
                    Mathf.FloorToInt(worldX / chunkWorldSize),
                    0,
                    mapSize.X - 1
                );
                int cz = Mathf.Clamp(
                    Mathf.FloorToInt(worldZ / chunkWorldSize),
                    0,
                    mapSize.Y - 1
                );

                bool isManmade =
                    chunkTypes != null
                    && GetChunkData(cx, cz).chunkType
                        == ChunkData.ChunkType.ManMade;

                float y;
                if (isManmade)
                {
                    // Do NOT generate noise in man-made areas; leave baseline flat
                    y = 0f;
                    // Lock so smoothing won't change interior
                    lockedVertices[x, z] = true;
                }
                else
                {
                    // Simple noise; quantize by cellSize.Y if desired
                    // Adjust as needed for your vertical scale
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
        int width = mapSize.X;
        int height = mapSize.Y;

        if (width <= 0 || height <= 0)
        {
            chunkTypes = Array.Empty<ChunkData>();
            GD.PrintErr(
                "MeshTerrainGenerator: mapSize is invalid; chunkTypes cleared."
            );
            return;
        }

        int count = width * height;

        if (chunkTypes == null || chunkTypes.Length != count)
            chunkTypes = new ChunkData[count];

        // Fill defaults (Procedural everywhere)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = x + y * width;
                var defaultData = new ChunkData
                {
                    chunkCoordinates = new Vector2I(x, y),
                    chunkType = ChunkData.ChunkType.Procedural
                };
                chunkTypes[idx] = defaultData;
            }
        }

        // Apply overrides from inspector only
        if (chunkOverrides == null || chunkOverrides.Length == 0)
            return;

        var seen = new HashSet<Vector2I>();
        for (int i = 0; i < chunkOverrides.Length; i++)
        {
            var ov = chunkOverrides[i];
            if (ov == null)
                continue;

            Vector2I coords = ov.chunkCoordinates;
            if (
                coords.X < 0
                || coords.Y < 0
                || coords.X >= width
                || coords.Y >= height
            )
            {
                GD.PrintErr(
                    $"Chunk override [{i}] has out-of-range coords {coords}. "
                        + $"Valid range: X:[0..{width - 1}] Y:[0..{height - 1}]. "
                        + "Skipping."
                );
                continue;
            }

            int idx = coords.X + coords.Y * width;
            
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
        int width = verts.GetLength(0);
        int height = verts.GetLength(1);
        for (int pass = 0; pass < validationPasses; pass++)
        {
            // Smooth out big differences in a 2x2 cell
            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    if (lockedVertices[x, y])
                        continue;

                    float[] cellHeights =
                    {
                        verts[x, y].Y,
                        verts[x + 1, y].Y,
                        verts[x, y + 1].Y,
                        verts[x + 1, y + 1].Y
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
                                        verts[x, y].Y = replacementHeight;
                                        break;
                                    case 1:
                                        verts[x + 1, y].Y = replacementHeight;
                                        break;
                                    case 2:
                                        verts[x, y + 1].Y = replacementHeight;
                                        break;
                                    case 3:
                                        verts[x + 1, y + 1].Y =
                                            replacementHeight;
                                        break;
                                }
                            }
                        }
                    }
                    else if (
                        uniqueHeights == 2
                        && verts[x, y].Y == verts[x + 1, y + 1].Y
                        && verts[x + 1, y].Y == verts[x, y + 1].Y
                    )
                    {
                        // Fix checkerboard seams
                        verts[x + 1, y + 1] = new Vector3(
                            verts[x + 1, y + 1].X,
                            verts[x, y].Y,
                            verts[x + 1, y + 1].Z
                        );
                    }
                }
            }
        }
    }

    #endregion

    #region Chunk Node Management

    private void EnsureChunkNodeExists(int x, int y)
    {
        ChunkData cData = GetChunkData(x, y);
        if (cData == null)
        {
            GD.PrintErr($"Null chunk data at {x}, {y}");
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

                    chunkNode = new Node3D { Name = $"Chunk_{x}_{y}" };
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

                        chunkNode = new Node3D { Name = $"Chunk_{x}_{y}" };
                        AddChild(chunkNode, forceReadableName: true);
                    }
                    else
                    {
                        chunkNode = chunkScene.Instantiate<Node3D>();
                        chunkNode.Name = $"Chunk_{x}_{y}";
                        AddChild(chunkNode, forceReadableName: true);
                    }
                }
            }
            else
            {
                chunkNode = new Node3D { Name = $"Chunk_{x}_{y}" };
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
        int totalWidth = mapSize.X * chunkSize + 1;
        int totalHeight = mapSize.Y * chunkSize + 1;

        for (int y = 0; y < totalHeight; y++)
        {
            for (int x = 0; x < totalWidth; x++)
            {
                bool isVerticalBoundary = (x % chunkSize == 0 && x != 0);
                bool isHorizontalBoundary = (y % chunkSize == 0 && y != 0);

                if (!isVerticalBoundary && !isHorizontalBoundary)
                    continue;

                ChunkData currentChunk = GetChunkFromVertexIndex(x, y);
                if (currentChunk == null)
                    continue;

                // Check vertical boundary: between left and right chunks
                if (isVerticalBoundary)
                {
                    int leftX = x - 1;
                    ChunkData leftChunk = GetChunkFromVertexIndex(leftX, y);

                    if (leftChunk != null)
                    {
                        bool boundaryBetweenManmadeAndProc =
                            (currentChunk.chunkType
                                 == ChunkData.ChunkType.ManMade
                                && leftChunk.chunkType
                                   == ChunkData.ChunkType.Procedural)
                            || (currentChunk.chunkType
                                    == ChunkData.ChunkType.Procedural
                                && leftChunk.chunkType
                                   == ChunkData.ChunkType.ManMade);

                        if (boundaryBetweenManmadeAndProc)
                        {
                            if (
                                TryRaycastManmadeHeightAtVertex(
                                    x,
                                    y,
                                    out float height
                                )
                            )
                            {
                                Vector3 v = terrainHeights[x, y];
                                v.Y = height;
                                terrainHeights[x, y] = v;
                                lockedVertices[x, y] = true;
                            }
                        }
                    }
                }

                // Check horizontal boundary: between below and above chunks
                if (isHorizontalBoundary)
                {
                    int belowY = y - 1;
                    ChunkData belowChunk = GetChunkFromVertexIndex(x, belowY);

                    if (belowChunk != null)
                    {
                        bool boundaryBetweenManmadeAndProc =
                            (currentChunk.chunkType
                                 == ChunkData.ChunkType.ManMade
                                && belowChunk.chunkType
                                   == ChunkData.ChunkType.Procedural)
                            || (currentChunk.chunkType
                                    == ChunkData.ChunkType.Procedural
                                && belowChunk.chunkType
                                   == ChunkData.ChunkType.ManMade);

                        if (boundaryBetweenManmadeAndProc)
                        {
                            if (
                                TryRaycastManmadeHeightAtVertex(
                                    x,
                                    y,
                                    out float height
                                )
                            )
                            {
                                Vector3 v = terrainHeights[x, y];
                                v.Y = height;
                                terrainHeights[x, y] = v;
                                lockedVertices[x, y] = true;
                            }
                        }
                    }
                }
            }
        }
    }

    private bool TryRaycastManmadeHeightAtVertex(int vertexX, int vertexY, out float height)
    {
        float worldX = vertexX * cellSize.X;
        float worldZ = vertexY * cellSize.X;
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
        // By default Godot collides with bodies; keep it true.
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

    public ChunkData GetChunkFromVertexIndex(int vertexX, int vertexY)
    {
        // Map a vertex (grid coordinate) to a chunk index.
        // For the last column/row we clamp to the last cell so division stays in range.
        if (vertexX < 0)
            vertexX = 0;
        else if (vertexX >= mapSize.X * chunkSize)
            vertexX = mapSize.X * chunkSize - 1;

        if (vertexY < 0)
            vertexY = 0;
        else if (vertexY >= mapSize.Y * chunkSize)
            vertexY = mapSize.Y * chunkSize - 1;

        int chunkX = vertexX / chunkSize;
        int chunkY = vertexY / chunkSize;

        if (chunkX < 0 || chunkX >= mapSize.X)
            return null;
        if (chunkY < 0 || chunkY >= mapSize.Y)
            return null;
        return GetChunkData(chunkX, chunkY);
    }

    public bool IsManMadeChunkAtWorld(float worldX, float worldZ)
    {
        if (chunkTypes == null)
            return false;

        float chunkWorldSize = chunkSize * cellSize.X;
        int cx = Mathf.FloorToInt(worldX / chunkWorldSize);
        int cy = Mathf.FloorToInt(worldZ / chunkWorldSize);

        if (cx < 0 || cy < 0 || cx >= mapSize.X || cy >= mapSize.Y)
            return false;

        return GetChunkData(cx, cy).chunkType
            == ChunkData.ChunkType.ManMade;
    }

    public float SampleHeightAtCellCenter(int cellX, int cellZ)
    {
        return SampleHeightAtCellCenterWithManmade(cellX, cellZ);
    }

    public float SampleHeightAtCellCenterWithManmade(int cellX, int cellZ)
    {
	    // Sample at the actual cell center, not the corner
	    float worldX = (cellX) * cellSize.X;
	    float worldZ = (cellZ ) * cellSize.X;
	    return SampleHeightAtWorld(worldX, worldZ);
    }

    public float SampleHeightAtWorld(float worldX, float worldZ)
    {
        return SampleHeightAtWorldWithManmade(worldX, worldZ);
    }

    public float SampleHeightAtWorldWithManmade(float worldX, float worldZ)
    {
        if (IsManMadeChunkAtWorld(worldX, worldZ))
        {
            if (
                TryRaycastManmadeHeightAtWorld(worldX, worldZ, out float h)
            )
                return h;

            // No hit: fall back to heightmap value
            return SampleHeightFromHeightmap(worldX, worldZ);
        }

        // Procedural area: just read from heightmap
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
            Mathf.RoundToInt(worldZ / cellSize.X),
            0,
            terrainHeights.GetLength(1) - 1
        );
        return terrainHeights[ix, iz].Y;
    }

    #endregion

    #region Get/Set Functions

    public ChunkData GetChunkData(int x, int y) =>
        chunkTypes[x + y * mapSize.X];

    private string GetChunkPrefabPath(string chunkId)
    {
        return $"res://Scenes/Chunks/{chunkId}.tscn";
    }

    public Vector2I GetMapSize() => mapSize;
    public int GetChunkSize() => chunkSize;
    public Vector2 GetCellSize() => cellSize;

    public Vector3I GetMapCellSize() =>
        new Vector3I(
            mapSize.X * chunkSize,
            chunkSize,
            mapSize.Y * chunkSize
        );

    public bool HasHeightData => terrainHeights != null;
	
    public ChunkData[] GetChunks() => chunkTypes;
    #endregion
}