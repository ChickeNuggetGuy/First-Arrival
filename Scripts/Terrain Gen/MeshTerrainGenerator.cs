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

	[Export] private bool generateTerrainMesh = true;
	[Export] public int chunkSize { get; set; }

	[Export] public Vector2 cellSize { get; set; }

	[Export] private int minHeightY = 0;
	[Export] private int maxHeightY = 6;
	[Export] private Material chunkMaterial { get; set; }

	public Vector3[,] terrainHeights { get; set; }

	[ExportGroup("Chunk Overrides")]
	[Export]
	private ChunkData[] chunkOverrides { get; set; } = Array.Empty<ChunkData>();

	[ExportGroup("Raycast Sampling")]
	[Export]
	private float manmadeRaycastHeight { get; set; } = 5000f;

	[Export] private float manmadeRaycastLength { get; set; } = 10000f;

	[Export] private uint manmadeRaycastMask { get; set; } = 0;

	[ExportGroup("Man-made Blending")]
	[Export]
	private int blendRadiusCells { get; set; } = 6;

	[Export] private float blendExponent { get; set; } = 1.0f;

	private ChunkData[] chunkTypes;
	private bool[,] lockedVertices;

	#endregion

	#region Setup and Execution

	public override string GetManagerName() => "TerrainGenerator";

	protected override async Task _Setup(bool loadingData)
	{
		if (generateTerrainMesh)
		{
			BuildChunkTypesFromOverrides();
			GenerateHeightMap();
			GD.Print("MeshTerrainGenerator: base height data ready.");
		}

		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		if (generateTerrainMesh)
		{
			GameManager gameManager = GameManager.Instance;

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
						chunkZ * chunkWorldSize
					);
				}
			}

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
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Seed = (int)GD.Randi(),
			Frequency = 1.0f / (20f * cellSize.X),
		};
		noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		noise.FractalOctaves = 3;
		noise.FractalLacunarity = 2.0f;
		noise.FractalGain = 0.4f;

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
					// Sample with WORLD coordinates
					float rawNoise = noise.GetNoise2D(worldX, worldZ);

					// Remap [-1,1] -> [0,1]
					float normalized = (rawNoise + 1f) * 0.5f;

					// Power curve: flat valleys, gentle hills
					float shaped = Mathf.Pow(normalized, 2.5f);

					y = shaped * maxHeightY;
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
		int chunksZ = gameManager.mapSize.Y; // North-South 

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

	public void ValidateHeights(
		Vector3[,] verts,
		int passes,
		float maxStepHeight
	)
	{
		int vertsX = verts.GetLength(0);
		int vertsZ = verts.GetLength(1);
		float step = cellSize.Y;

		for (int pass = 0; pass < passes; pass++)
		{
			// --- Pass A: Clamp max height difference between neighbors ---
			for (int z = 0; z < vertsZ; z++)
			{
				for (int x = 0; x < vertsX; x++)
				{
					if (lockedVertices[x, z])
						continue;

					float h = verts[x, z].Y;

					// Check right neighbor
					if (x + 1 < vertsX)
						h = ClampTowardNeighbor(
							h, verts[x + 1, z].Y, maxStepHeight, step
						);

					// Check forward neighbor
					if (z + 1 < vertsZ)
						h = ClampTowardNeighbor(
							h, verts[x, z + 1].Y, maxStepHeight, step
						);

					// Check left neighbor
					if (x - 1 >= 0)
						h = ClampTowardNeighbor(
							h, verts[x - 1, z].Y, maxStepHeight, step
						);

					// Check back neighbor
					if (z - 1 >= 0)
						h = ClampTowardNeighbor(
							h, verts[x, z - 1].Y, maxStepHeight, step
						);

					verts[x, z] = new Vector3(verts[x, z].X, h, verts[x, z].Z);
				}
			}

			// --- Pass B: Fix invalid cell patterns ---
			for (int z = 0; z < vertsZ - 1; z++)
			{
				for (int x = 0; x < vertsX - 1; x++)
				{
					float bl = verts[x, z].Y; // bottom-left
					float br = verts[x + 1, z].Y; // bottom-right
					float tl = verts[x, z + 1].Y; // top-left
					float tr = verts[x + 1, z + 1].Y; // top-right

					float[] heights = { bl, br, tl, tr };
					int uniqueCount = heights.Distinct().Count();

					if (uniqueCount <= 1)
						continue; // flat cell, fine

					if (uniqueCount >= 3)
					{
						// 3 or 4 unique heights — snap outliers to majority
						FixCellToMajority(verts, x, z, heights);
						continue;
					}

					// Exactly 2 unique heights — check for invalid patterns
					// Invalid: 3 share one height, 1 is different (ramp with
					// a notch)
					var groups = heights
						.GroupBy(h => h)
						.OrderByDescending(g => g.Count())
						.ToList();

					if (groups[0].Count() == 3)
					{
						// 3+1 pattern — snap the outlier
						FixCellToMajority(verts, x, z, heights);
						continue;
					}

					// 2+2 pattern — check if diagonal (saddle)
					bool isSaddle =
						Mathf.IsEqualApprox(bl, tr)
						&& Mathf.IsEqualApprox(br, tl)
						&& !Mathf.IsEqualApprox(bl, br);

					if (isSaddle)
					{
						// Resolve saddle: pick the lower height for the
						// minority pair (prefer flatter terrain)
						float keep = Mathf.Min(bl, br);
						SetCellHeight(verts, x, z, keep);
					}

					// 2+2 along an edge (adjacent split) is valid — a clean
					// ramp, so we leave it alone.
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

		// Pull current toward neighbor so the gap is exactly maxDiff
		float clamped = diff > 0
			? neighbor + maxDiff
			: neighbor - maxDiff;

		// Re-quantize if using grid snapping
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
		// Find the most common height in the cell
		float majority = heights
			.GroupBy(h => h)
			.OrderByDescending(g => g.Count())
			.First()
			.Key;

		// Only fix unlocked vertices
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
				verts[x + 1, z].X, height, verts[x + 1, z].Z
			);
		if (!lockedVertices[x, z + 1])
			verts[x, z + 1] = new Vector3(
				verts[x, z + 1].X, height, verts[x, z + 1].Z
			);
		if (!lockedVertices[x + 1, z + 1])
			verts[x + 1, z + 1] = new Vector3(
				verts[x + 1, z + 1].X, height, verts[x + 1, z + 1].Z
			);
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

	private bool TryRaycastManmadeHeightAtVertex(
		int vertexX,
		int vertexZ,
		out float height
	)
	{
		float worldX = vertexX * cellSize.X;
		float worldZ = vertexZ * cellSize.X;
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
		int chunkZ = Mathf.FloorToInt(worldZ / chunkWorldSize);

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
		float worldZ = cellZ * cellSize.X;
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

	private string GetChunkPrefabPath(string chunkId)
	{
		return $"res://Scenes/Chunks/{chunkId}.tscn";
	}

	public Vector2I GetMapSize() => GameManager.Instance.mapSize;
	public int GetChunkSize() => chunkSize;
	public Vector2 GetCellSize() => cellSize;

	public Vector3I GetMapCellSize() =>
		new Vector3I(
			GameManager.Instance.mapSize.X * chunkSize,
			chunkSize,
			GameManager.Instance.mapSize.Y * chunkSize
		);

	public bool HasHeightData => terrainHeights != null;

	public ChunkData[] GetChunks() => chunkTypes;


	#region manager Data

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		if (terrainHeights == null || lockedVertices == null)
		{
			GD.PrintErr("MeshTerrainGenerator: Cannot save, data is null.");
			return new Godot.Collections.Dictionary<string, Variant>();
		}

		int width = terrainHeights.GetLength(0);
		int depth = terrainHeights.GetLength(1);

		// Flatten the 2D heightmap into a 1D float array
		float[] flatHeights = new float[width * depth];

		// Flatten the 2D locked boolean map into a 1D BYTE array
		byte[] flatLocked = new byte[width * depth];

		for (int x = 0; x < width; x++)
		{
			for (int z = 0; z < depth; z++)
			{
				int index = x + (z * width);
				flatHeights[index] = terrainHeights[x, z].Y;

				// Convert bool to byte (1 for true, 0 for false)
				flatLocked[index] = lockedVertices[x, z] ? (byte)1 : (byte)0;
			}
		}

		var data = new Godot.Collections.Dictionary<string, Variant>
		{
			{ "GridWidth", width },
			{ "GridDepth", depth },
			{ "Heights", flatHeights },
			{ "LockedVertices", flatLocked }
		};

		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		base.Load(data);
		if (!HasLoadedData) return;

		if (data == null || !data.ContainsKey("Heights"))
		{
			HasLoadedData = false;
			return;
		}

		if (!data.ContainsKey("GridWidth") || !data.ContainsKey("GridDepth") || !data.ContainsKey("Heights"))
		{
			GD.PrintErr("MeshTerrainGenerator: Save data is missing critical keys.");
			return;
		}

		// 1. Retrieve Dimensions
		int width = data["GridWidth"].As<int>();
		int depth = data["GridDepth"].As<int>();

		// 2. Retrieve Arrays
		float[] flatHeights = data["Heights"].As<float[]>();

		// Retrieve Bytes and prepare to convert back to bool
		byte[] flatLocked = null;
		if (data.ContainsKey("LockedVertices"))
		{
			flatLocked = data["LockedVertices"].As<byte[]>();
		}

		// 3. Re-initialize the 2D arrays
		terrainHeights = new Vector3[width, depth];
		lockedVertices = new bool[width, depth];

		// 4. Reconstruct the Vector3 grid
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
				{
					// Convert byte back to bool
					lockedVertices[x, z] = flatLocked[index] > 0;
				}
			}
		}

		GD.Print("MeshTerrainGenerator: Terrain heightmap loaded successfully.");
	}

	#endregion

	#endregion

	public override void Deinitialize()
	{
		return;
	}
}