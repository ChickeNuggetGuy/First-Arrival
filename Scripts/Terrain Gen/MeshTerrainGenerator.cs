using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class MeshTerrainGenerator : Manager<MeshTerrainGenerator>
{
	#region Variables

	[Export] private bool generateTerrainMesh = true;
	[Export] public int chunkSize { get; set; }

	[Export] public Vector2 cellSize { get; set; }

	[Export] private int minHeightY = 0;
	[Export] private int maxHeightY = 6;

	[Export] private Enums.ChunkType mapType;
	[Export] private Material chunkMaterial { get; set; }

	public Vector3[,] terrainHeights { get; set; }

	[ExportGroup("Chunk Overrides")]
	[Export]
	private ChunkData[] chunkOverrides { get; set; }

	[ExportGroup("Chunk Prefab Loading")]
	[Export]
	private string chunksRootFolder { get; set; } = "res://Scenes/Chunks";

	[Export] private bool autoLoadChunkPrefabsFromFolders { get; set; } = true;

	[ExportGroup("Urban Spawning Performance")]
	[Export]
	private int manmadeSpawnBudgetPerFrame { get; set; } = 2;

	[Export] private bool logManmadeLoads { get; set; } = false;

	[ExportGroup("Raycast Sampling")]
	[Export]
	private float manmadeRaycastHeight { get; set; } = 5000f;

	[Export] private float manmadeRaycastLength { get; set; } = 10000f;

	[Export] private uint manmadeRaycastMask { get; set; } = 0;

	[ExportGroup("Man-made Blending")]
	[Export]
	private int blendRadiusCells { get; set; } = 6;

	[Export] private float blendExponent { get; set; } = 1.0f;

	[Export] private Godot.Collections.Dictionary<Enums.ChunkType, Array> chunkPrefabs =
		new();

	private Array<ChunkData> chunkTypes;
	private bool[,] lockedVertices;
	private readonly RandomNumberGenerator rng = new();

	private readonly System.Collections.Generic.Dictionary<string, PackedScene> packedSceneCache = new();

	#endregion

	#region Setup and Execution

	public override string GetManagerName() => "TerrainGenerator";

	protected override async Task _Setup(bool loadingData)
	{
		switch (mapType)
		{
			case Enums.ChunkType.Grassland:
			case Enums.ChunkType.Forest:
			case Enums.ChunkType.Mountain:
				generateTerrainMesh = true;
				break;
			case Enums.ChunkType.Urban:
				generateTerrainMesh = false;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		rng.Randomize();

		if (autoLoadChunkPrefabsFromFolders)
			PopulateChunkPrefabPathsFromFolders();

		BuildChunkTypesForCurrentMap();

		if (generateTerrainMesh)
		{
			GenerateHeightMap();
			GD.Print("MeshTerrainGenerator: base height data ready.");
		}

		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		GameManager gameManager = GameManager.Instance;

		int manmadeCreatedThisFrame = 0;
		int budget = Mathf.Max(1, manmadeSpawnBudgetPerFrame);

		// Always ensure chunk nodes exist and position them (procedural or man-made)
		for (int chunkX = 0; chunkX < gameManager.mapSize.X; chunkX++)
		{
			for (int chunkZ = 0; chunkZ < gameManager.mapSize.Y; chunkZ++)
			{
				ChunkData cData = GetChunkData(chunkX, chunkZ);

				bool willInstantiate =
					cData != null
					&& cData.GetChunkNode() == null
					&& cData.chunkType == ChunkData.ChunkType.ManMade;

				EnsureChunkNodeExists(chunkX, chunkZ);

				var node = cData?.GetChunkNode();
				if (node != null)
				{
					float chunkWorldSize = chunkSize * cellSize.X;

					node.Position = new Vector3(
						chunkX * chunkWorldSize,
						0f,
						chunkZ * chunkWorldSize
					);
				}

				// Throttle heavy scene instantiation so the game doesn't look frozen.
				if (willInstantiate)
				{
					manmadeCreatedThisFrame++;
					if (manmadeCreatedThisFrame >= budget)
					{
						manmadeCreatedThisFrame = 0;
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					}
				}
			}
		}

		// Only generate mesh terrain when procedural terrain is enabled
		if (generateTerrainMesh)
		{
			if (!HasLoadedData)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

				BlendHeightsToZeroAroundManmade();

				ValidateHeights(terrainHeights, 1, cellSize.Y * 2f);

				ClampHeightsToMin(minHeightY, includeManMade: false);
			}

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
					cData.chunk.Generate(chunkMaterial);
				}
			}

			GD.Print($"MeshTerrainGenerator: chunks built. {GetMapSize()}");
		}

		await Task.CompletedTask;
	}

	#endregion

	#region Chunk Prefabs (folder loading)

	private void PopulateChunkPrefabPathsFromFolders()
	{
		chunkPrefabs.Clear();

		foreach (Enums.ChunkType type in Enum.GetValues(typeof(Enums.ChunkType)))
		{
			string folder = $"{chunksRootFolder}/{type}";
			DirAccess dir = DirAccess.Open(folder);
			if (dir == null)
				continue;

			var paths = new Array();

			// Some Godot C# bindings don’t expose named params, so call and filter.
			dir.ListDirBegin();
			while (true)
			{
				string file = dir.GetNext();
				if (file == "")
					break;

				if (file == "." || file == "..")
					continue;

				if (file.StartsWith(".", StringComparison.Ordinal))
					continue;

				if (dir.CurrentIsDir())
					continue;

				bool isScene =
					file.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
					|| file.EndsWith(".scn", StringComparison.OrdinalIgnoreCase);

				if (!isScene)
					continue;

				paths.Add($"{folder}/{file}");
			}

			dir.ListDirEnd();

			if (paths.Count > 0)
				chunkPrefabs[type] = paths;
		}

		GD.Print(
			$"MeshTerrainGenerator: Loaded prefab paths for {chunkPrefabs.Count} "
			+ "map type(s)."
		);
	}

	private bool TryGetPrefabListForMapType(Enums.ChunkType type, out Array list)
	{
		list = null;

		if (chunkPrefabs == null)
			return false;

		if (!chunkPrefabs.ContainsKey(type))
			return false;

		list = chunkPrefabs[type];
		return list != null && list.Count > 0;
	}

	private string ResolvePrefabVariantToPath(Variant v)
	{
		string s = v.AsString();
		if (!string.IsNullOrWhiteSpace(s))
			return s;

		PackedScene ps = v.As<PackedScene>();
		if (ps != null && !string.IsNullOrWhiteSpace(ps.ResourcePath))
			return ps.ResourcePath;

		return "";
	}

	#endregion

	#region Chunk Types (procedural vs urban)

	private void BuildChunkTypesForCurrentMap()
	{
		if (mapType == Enums.ChunkType.Urban)
		{
			BuildChunkTypesUrbanRandom();
			ApplyChunkOverridesOnTop();
			return;
		}

		BuildChunkTypesFromOverrides();
	}

	private void BuildChunkTypesUrbanRandom()
	{
		GameManager gameManager = GameManager.Instance;
		int chunksX = gameManager.mapSize.X;
		int chunksZ = gameManager.mapSize.Y;

		if (chunksX <= 0 || chunksZ <= 0)
		{
			chunkTypes = new Array<ChunkData>();
			GD.PrintErr("MeshTerrainGenerator: mapSize is invalid; chunkTypes cleared.");
			return;
		}

		int count = chunksX * chunksZ;

		if (chunkTypes == null)
			chunkTypes = new Array<ChunkData>();

		if (chunkTypes.Count != count)
			chunkTypes.Resize(count);

		if (!TryGetPrefabListForMapType(mapType, out Array prefabs))
		{
			GD.PrintErr(
				$"MeshTerrainGenerator: No prefabs registered for {mapType}. "
				+ $"Expected folder: {chunksRootFolder}/{mapType} "
				+ "or assign chunkPrefabs manually."
			);

			for (int z = 0; z < chunksZ; z++)
			{
				for (int x = 0; x < chunksX; x++)
				{
					int idx = x + z * chunksX;
					chunkTypes[idx] = new ChunkData
					{
						chunkCoordinates = new Vector2I(x, z),
						chunkType = ChunkData.ChunkType.ManMade,
						chunkGOIndex = ""
					};
				}
			}

			return;
		}

		for (int z = 0; z < chunksZ; z++)
		{
			for (int x = 0; x < chunksX; x++)
			{
				int idx = x + z * chunksX;

				int pick = rng.RandiRange(0, prefabs.Count - 1);
				string path = ResolvePrefabVariantToPath(prefabs[pick]);

				chunkTypes[idx] = new ChunkData
				{
					chunkCoordinates = new Vector2I(x, z),
					chunkType = ChunkData.ChunkType.ManMade,
					chunkGOIndex = path
				};
			}
		}
	}

	private void ApplyChunkOverridesOnTop()
	{
		if (chunkOverrides == null || chunkOverrides.Length == 0)
			return;

		GameManager gameManager = GameManager.Instance;
		int chunksX = gameManager.mapSize.X;
		int chunksZ = gameManager.mapSize.Y;

		for (int i = 0; i < chunkOverrides.Length; i++)
		{
			var ov = chunkOverrides[i];
			if (ov == null)
				continue;

			Vector2I coords = ov.chunkCoordinates;
			if (
				coords.X < 0
				|| coords.Y < 0
				|| coords.X >= chunksX
				|| coords.Y >= chunksZ
			)
			{
				GD.PrintErr($"Chunk override [{i}] out-of-range coords {coords}.");
				continue;
			}

			int idx = coords.X + coords.Y * chunksX;

			ChunkData ovCopy = ov.Duplicate(true) as ChunkData;
			if (ovCopy == null)
			{
				ovCopy = new ChunkData
				{
					chunkCoordinates = ov.chunkCoordinates,
					chunkType = ov.chunkType,
					chunkGOIndex = ov.chunkGOIndex
				};
			}

			ovCopy.SetChunkNode(null);
			ovCopy.chunk = null;

			chunkTypes[idx] = ovCopy;
		}
	}

	#endregion

	#region Heightmap Generation

	public void GenerateHeightMap()
	{
		GameManager gameManager = GameManager.Instance;

		int vertsX = (gameManager.mapSize.X * chunkSize) + 1;
		int vertsZ = (gameManager.mapSize.Y * chunkSize) + 1;

		terrainHeights = new Vector3[vertsX, vertsZ];
		lockedVertices = new bool[vertsX, vertsZ];

		FastNoiseLite noise = new FastNoiseLite
		{
			Seed = (int)GD.Randi(),
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 1.0f / (80f * cellSize.X),
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 2, 
			FractalLacunarity = 2.0f,
			FractalGain = 0.5f
		};
		
		


		float chunkWorldSize = chunkSize * cellSize.X;

		for (int x = 0; x < vertsX; x++)
		{
			for (int z = 0; z < vertsZ; z++)
			{
				float worldX = x * cellSize.X;
				float worldZ = z * cellSize.X;

				int chunkX = Mathf.Clamp(
					Mathf.FloorToInt(worldX / chunkWorldSize),
					0,
					gameManager.mapSize.X - 1
				);
				int chunkZ = Mathf.Clamp(
					Mathf.FloorToInt(worldZ / chunkWorldSize),
					0,
					gameManager.mapSize.Y - 1
				);

				bool isManmade =
					chunkTypes != null
					&& GetChunkData(chunkX, chunkZ).chunkType
					== ChunkData.ChunkType.ManMade;

				float y;
				if (isManmade)
				{
					y = 0f;
					lockedVertices[x, z] = true;
				}
				else
				{
					float rawNoise = noise.GetNoise2D(worldX, worldZ);
					float normalized = (rawNoise + 1f) * 0.5f;
					float shaped = Mathf.Pow(normalized, 1.8f);
					 y = shaped * maxHeightY * 0.6f;   
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

		float step = cellSize.Y;
		float minYQuantized = step > 0f ? Mathf.Ceil(minY / step) * step : minY;

		for (int z = 0; z < vertsZ; z++)
		{
			for (int x = 0; x < vertsX; x++)
			{
				if (
					!includeManMade
					&& lockedVertices != null
					&& lockedVertices[x, z]
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
		int chunksX = gameManager.mapSize.X;
		int chunksZ = gameManager.mapSize.Y;

		if (chunksX <= 0 || chunksZ <= 0)
		{
			chunkTypes = new Array<ChunkData>();
			GD.PrintErr("MeshTerrainGenerator: mapSize is invalid; chunkTypes cleared.");
			return;
		}

		int count = chunksX * chunksZ;

		if (chunkTypes == null)
			chunkTypes = new Array<ChunkData>();

		if (chunkTypes.Count != count)
			chunkTypes.Resize(count);

		for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++)
		{
			for (int chunkX = 0; chunkX < chunksX; chunkX++)
			{
				int idx = chunkX + chunkZ * chunksX;
				chunkTypes[idx] = new ChunkData
				{
					chunkCoordinates = new Vector2I(chunkX, chunkZ),
					chunkType = ChunkData.ChunkType.Procedural
				};
			}
		}

		if (chunkOverrides == null || chunkOverrides.Length == 0)
			return;

		var seen = new HashSet<Vector2I>();
		for (int i = 0; i < chunkOverrides.Length; i++)
		{
			var ov = chunkOverrides[i];
			if (ov == null)
				continue;

			Vector2I coords = ov.chunkCoordinates;
			if (coords.X < 0 || coords.Y < 0 || coords.X >= chunksX || coords.Y >= chunksZ)
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
					chunkType = ov.chunkType,
					chunkGOIndex = ov.chunkGOIndex
				};
			}

			ovCopy.SetChunkNode(null);
			ovCopy.chunk = null;

			chunkTypes[idx] = ovCopy;

			if (!seen.Add(coords))
				GD.Print($"Duplicate override for coords {coords}; last wins.");
		}
	}

	public void ValidateHeights(Vector3[,] verts, int passes, float maxStepHeight)
	{
		int vertsX = verts.GetLength(0);
		int vertsZ = verts.GetLength(1);
		float step = cellSize.Y;

		for (int pass = 0; pass < passes; pass++)
		{
			for (int z = 0; z < vertsZ; z++)
			{
				for (int x = 0; x < vertsX; x++)
				{
					if (lockedVertices[x, z])
						continue;

					float h = verts[x, z].Y;

					if (x + 1 < vertsX)
						h = ClampTowardNeighbor(
							h,
							verts[x + 1, z].Y,
							maxStepHeight,
							step
						);

					if (z + 1 < vertsZ)
						h = ClampTowardNeighbor(
							h,
							verts[x, z + 1].Y,
							maxStepHeight,
							step
						);

					if (x - 1 >= 0)
						h = ClampTowardNeighbor(
							h,
							verts[x - 1, z].Y,
							maxStepHeight,
							step
						);

					if (z - 1 >= 0)
						h = ClampTowardNeighbor(
							h,
							verts[x, z - 1].Y,
							maxStepHeight,
							step
						);

					verts[x, z] = new Vector3(verts[x, z].X, h, verts[x, z].Z);
				}
			}

			for (int z = 0; z < vertsZ - 1; z++)
			{
				for (int x = 0; x < vertsX - 1; x++)
				{
					float bl = verts[x, z].Y;
					float br = verts[x + 1, z].Y;
					float tl = verts[x, z + 1].Y;
					float tr = verts[x + 1, z + 1].Y;

					float[] heights = { bl, br, tl, tr };
					int uniqueCount = heights.Distinct().Count();

					if (uniqueCount <= 1)
						continue;

					if (uniqueCount >= 3)
					{
						FixCellToMajority(verts, x, z, heights);
						continue;
					}

					var groups = heights
						.GroupBy(h => h)
						.OrderByDescending(g => g.Count())
						.ToList();

					if (groups[0].Count() == 3)
					{
						FixCellToMajority(verts, x, z, heights);
						continue;
					}

					bool isSaddle =
						Mathf.IsEqualApprox(bl, tr)
						&& Mathf.IsEqualApprox(br, tl)
						&& !Mathf.IsEqualApprox(bl, br);

					if (isSaddle)
					{
						float keep = Mathf.Min(bl, br);
						SetCellHeight(verts, x, z, keep);
					}
				}
			}
		}
	}

	private float ClampTowardNeighbor(
		float current,
		float neighbor,
		float maxDiff,
		float quantizeStep
	)
	{
		float diff = current - neighbor;

		if (Mathf.Abs(diff) <= maxDiff)
			return current;

		float clamped = diff > 0 ? neighbor + maxDiff : neighbor - maxDiff;

		if (quantizeStep > 0f)
			clamped = Mathf.Round(clamped / quantizeStep) * quantizeStep;

		return clamped;
	}

	private void FixCellToMajority(
		Vector3[,] verts,
		int x,
		int z,
		float[] heights
	)
	{
		float majority = heights
			.GroupBy(h => h)
			.OrderByDescending(g => g.Count())
			.First()
			.Key;

		if (!lockedVertices[x, z] && !Mathf.IsEqualApprox(heights[0], majority))
			verts[x, z] = new Vector3(verts[x, z].X, majority, verts[x, z].Z);

		if (
			!lockedVertices[x + 1, z]
			&& !Mathf.IsEqualApprox(heights[1], majority)
		)
			verts[x + 1, z] = new Vector3(
				verts[x + 1, z].X,
				majority,
				verts[x + 1, z].Z
			);

		if (
			!lockedVertices[x, z + 1]
			&& !Mathf.IsEqualApprox(heights[2], majority)
		)
			verts[x, z + 1] = new Vector3(
				verts[x, z + 1].X,
				majority,
				verts[x, z + 1].Z
			);

		if (
			!lockedVertices[x + 1, z + 1]
			&& !Mathf.IsEqualApprox(heights[3], majority)
		)
			verts[x + 1, z + 1] = new Vector3(
				verts[x + 1, z + 1].X,
				majority,
				verts[x + 1, z + 1].Z
			);
	}

	private void SetCellHeight(Vector3[,] verts, int x, int z, float height)
	{
		if (!lockedVertices[x, z])
			verts[x, z] = new Vector3(verts[x, z].X, height, verts[x, z].Z);
		if (!lockedVertices[x + 1, z])
			verts[x + 1, z] = new Vector3(
				verts[x + 1, z].X,
				height,
				verts[x + 1, z].Z
			);
		if (!lockedVertices[x, z + 1])
			verts[x, z + 1] = new Vector3(
				verts[x, z + 1].X,
				height,
				verts[x, z + 1].Z
			);
		if (!lockedVertices[x + 1, z + 1])
			verts[x + 1, z + 1] = new Vector3(
				verts[x + 1, z + 1].X,
				height,
				verts[x + 1, z + 1].Z
			);
	}

	#endregion

	#region Chunk Node Management

	private PackedScene LoadPackedSceneCached(string prefabPath)
	{
		if (string.IsNullOrWhiteSpace(prefabPath))
			return null;

		if (packedSceneCache.TryGetValue(prefabPath, out PackedScene cached))
			return cached;

		PackedScene loaded = ResourceLoader.Load<PackedScene>(prefabPath);
		if (loaded != null)
			packedSceneCache[prefabPath] = loaded;

		return loaded;
	}

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
				if (logManmadeLoads)
					GD.Print($"Loading ManMade chunk from: {prefabPath}");

				if (string.IsNullOrWhiteSpace(prefabPath))
				{
					GD.PrintErr(
						$"Empty prefab path for ManMade chunk at {chunkX},{chunkZ}. "
						+ "Spawning empty node."
					);

					chunkNode = new Node3D { Name = $"Chunk_{chunkX}_{chunkZ}" };
					AddChild(chunkNode, forceReadableName: true);
				}
				else
				{
					PackedScene chunkScene = LoadPackedSceneCached(prefabPath);
					if (chunkScene == null)
					{
						GD.PrintErr($"Failed to load PackedScene at {prefabPath}.");

						if (generateTerrainMesh)
							cData.chunkType = ChunkData.ChunkType.Procedural;

						chunkNode = new Node3D { Name = $"Chunk_{chunkX}_{chunkZ}" };
						AddChild(chunkNode, forceReadableName: true);
					}
					else
					{
						// If Instantiate() is where you hang, it’s almost always
						// because the scene’s _Ready() (or tools) is doing heavy work.
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
			EnsureChunkComponentFast(cData);
		}
		else
		{
			EnsureChunkComponentFast(cData);
		}
	}

	private void EnsureChunkComponentFast(ChunkData cData)
	{
		Node3D node = cData.GetChunkNode();
		if (node == null)
			return;

		if (node is Chunk rootChunk)
		{
			cData.chunk = rootChunk;
			return;
		}

		// Avoid any deep recursive "GetOrCreateChildOfType" scan on big city scenes.
		// Only look at direct children.
		for (int i = 0; i < node.GetChildCount(); i++)
		{
			if (node.GetChild(i) is Chunk existing)
			{
				cData.chunk = existing;
				return;
			}
		}

		var comp = new Chunk { Name = "Chunk" };
		node.AddChild(comp);
		cData.chunk = comp;
	}

	#endregion

	#region Man-made Blending

	private static float Smooth01(float t)
	{
		t = Mathf.Clamp(t, 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	private void BlendHeightsToZeroAroundManmade()
	{
		if (terrainHeights == null || chunkTypes == null || chunkTypes.Count == 0)
			return;

		int vertsX = terrainHeights.GetLength(0);
		int vertsZ = terrainHeights.GetLength(1);

		float[,] weights = new float[vertsX, vertsZ];
		for (int z = 0; z < vertsZ; z++)
		{
			for (int x = 0; x < vertsX; x++)
				weights[x, z] = 1f;
		}

		int radius = Mathf.Max(1, blendRadiusCells);

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

		foreach (var rect in manmadeChunks)
		{
			int vx0 = rect.vx0;
			int vz0 = rect.vz0;
			int vx1 = rect.vx1;
			int vz1 = rect.vz1;

			int ex0 = Mathf.Clamp(vx0 - radius, 0, vertsX - 1);
			int ez0 = Mathf.Clamp(vz0 - radius, 0, vertsZ - 1);
			int ex1 = Mathf.Clamp(vx1 + radius, 0, vertsX - 1);
			int ez1 = Mathf.Clamp(vz1 + radius, 0, vertsZ - 1);

			for (int z = ez0; z <= ez1; z++)
			{
				for (int x = ex0; x <= ex1; x++)
				{
					int dx =
						(x < vx0) ? (vx0 - x) : (x > vx1) ? (x - vx1) : 0;
					int dz =
						(z < vz0) ? (vz0 - z) : (z > vz1) ? (z - vz1) : 0;

					float dist = Mathf.Sqrt(dx * dx + dz * dz);
					if (dist > radius)
						continue;

					float t = dist / radius;
					if (!Mathf.IsEqualApprox(blendExponent, 1.0f))
						t = Mathf.Pow(t, Mathf.Max(0.0001f, blendExponent));
					float factor = Smooth01(t);

					if (factor < weights[x, z])
						weights[x, z] = factor;
				}
			}
		}

		for (int z = 0; z < vertsZ; z++)
		{
			for (int x = 0; x < vertsX; x++)
			{
				float w = weights[x, z];

				if (lockedVertices[x, z])
				{
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

					newY = Mathf.Round(newY / cellSize.Y) * cellSize.Y;
					v.Y = newY;
					terrainHeights[x, z] = v;

					if (w <= 0.0001f)
						lockedVertices[x, z] = true;
				}
			}
		}

		GD.Print(
			$"MeshTerrainGenerator: Applied zero-blend radius {radius} cells "
			+ $"around {manmadeChunks.Count} man-made chunk(s)."
		);
	}

	#endregion

	#region Man-made Border Baking (Legacy - not used)

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
		int chunkZ = Mathf.FloorToInt(worldZ / chunkWorldSize);

		if (
			chunkX < 0
			|| chunkZ < 0
			|| chunkX >= gameManager.mapSize.X
			|| chunkZ >= gameManager.mapSize.Y
		)
			return false;

		return GetChunkData(chunkX, chunkZ).chunkType == ChunkData.ChunkType.ManMade;
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

			if (terrainHeights == null)
				return 0f;

			return SampleHeightFromHeightmap(worldX, worldZ);
		}

		if (terrainHeights == null)
			return 0f;

		return SampleHeightFromHeightmap(worldX, worldZ);
	}

	private float SampleHeightFromHeightmap(float worldX, float worldZ)
	{
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

	public ChunkData GetChunkData(int chunkX, int chunkZ) =>
		chunkTypes[chunkX + chunkZ * GameManager.Instance.mapSize.X];

	private string GetChunkPrefabPath(string chunkIdOrPath)
	{
		if (string.IsNullOrWhiteSpace(chunkIdOrPath))
			return "";

		if (chunkIdOrPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
			return chunkIdOrPath;

		if (chunkIdOrPath.Contains("/"))
		{
			bool hasExt =
				chunkIdOrPath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
				|| chunkIdOrPath.EndsWith(".scn", StringComparison.OrdinalIgnoreCase);

			return hasExt
				? $"{chunksRootFolder}/{chunkIdOrPath}"
				: $"{chunksRootFolder}/{chunkIdOrPath}.tscn";
		}

		bool alreadyScene =
			chunkIdOrPath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
			|| chunkIdOrPath.EndsWith(".scn", StringComparison.OrdinalIgnoreCase);

		if (alreadyScene)
			return $"{chunksRootFolder}/{mapType}/{chunkIdOrPath}";

		return $"{chunksRootFolder}/{mapType}/{chunkIdOrPath}.tscn";
	}

	public Vector2I GetMapSize() => GameManager.Instance.mapSize;

	#endregion

	#region manager Data

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		if (!generateTerrainMesh)
			return new Godot.Collections.Dictionary<string, Variant>();

		if (terrainHeights == null || lockedVertices == null)
		{
			GD.PrintErr("MeshTerrainGenerator: Cannot save, data is null.");
			return new Godot.Collections.Dictionary<string, Variant>();
		}

		int width = terrainHeights.GetLength(0);
		int depth = terrainHeights.GetLength(1);

		float[] flatHeights = new float[width * depth];
		byte[] flatLocked = new byte[width * depth];

		for (int x = 0; x < width; x++)
		{
			for (int z = 0; z < depth; z++)
			{
				int index = x + (z * width);
				flatHeights[index] = terrainHeights[x, z].Y;
				flatLocked[index] = lockedVertices[x, z] ? (byte)1 : (byte)0;
			}
		}

		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "GridWidth", width },
			{ "GridDepth", depth },
			{ "Heights", flatHeights },
			{ "LockedVertices", flatLocked }
		};
	}

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!HasLoadedData)
			return Task.CompletedTask;

		if (!generateTerrainMesh)
			return Task.CompletedTask;

		if (data == null || !data.ContainsKey("Heights"))
		{
			HasLoadedData = false;
			return Task.CompletedTask;
		}

		int width = data["GridWidth"].As<int>();
		int depth = data["GridDepth"].As<int>();
		float[] flatHeights = data["Heights"].As<float[]>();

		byte[] flatLocked = null;
		if (data.ContainsKey("LockedVertices"))
			flatLocked = data["LockedVertices"].As<byte[]>();

		terrainHeights = new Vector3[width, depth];
		lockedVertices = new bool[width, depth];

		for (int x = 0; x < width; x++)
		{
			for (int z = 0; z < depth; z++)
			{
				int index = x + (z * width);

				float worldX = x * cellSize.X;
				float worldZ = z * cellSize.X;
				float loadedY = flatHeights[index];

				terrainHeights[x, z] = new Vector3(worldX, loadedY, worldZ);

				if (flatLocked != null)
					lockedVertices[x, z] = flatLocked[index] > 0;
			}
		}

		GD.Print("MeshTerrainGenerator: Terrain heightmap loaded successfully.");
		return Task.CompletedTask;
	}

	#endregion

	public override void Deinitialize()
	{
		return;
	}

	public Vector3I GetMapCellSize()
	{
		return new Vector3I(Mathf.RoundToInt(cellSize.X) * chunkSize, Mathf.RoundToInt(cellSize.Y) * chunkSize,
			Mathf.RoundToInt(cellSize.X) * chunkSize);
	}
}