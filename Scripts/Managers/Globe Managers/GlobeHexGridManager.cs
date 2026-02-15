using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GlobeHexGridManager : Manager<GlobeHexGridManager>
{
	[Export] private int resolution = 4;
	[Export] public float Radius = 10.0f;


	[Export] public Vector2 latLonOffset = Vector2.Zero;

	[Export] private Godot.Collections.Dictionary<string, Vector2> textureOffsets =
		new Godot.Collections.Dictionary<string, Vector2>();

	[Export] private Godot.Collections.Dictionary<string, Texture2D> textures =
		new Godot.Collections.Dictionary<string, Texture2D>();


	public System.Collections.Generic.Dictionary<Vector2, HexCellData> Cells = new();

	private HexCellData[] _cellArray;

	private ConcurrentDictionary<Vector3I, ConcurrentBag<int>> _spatialHash = new();
	private float _hashSize;

	private uint[] _countryKeyByIndex = Array.Empty<uint>();
	private Dictionary<uint, int[]> _countryToCellIndices = new();
	private long[]? _loadedCountryKeysFromSave;
	public int[] DebugHighlightedCellIndices { get; private set; } = Array.Empty<int>();


	private MeshInstance3D _debugHighlightMesh;
	private int _lastHighlightHash = 0;
	private bool _gridCreated = false;


	public override string GetManagerName() => "GlobeHexGridManager";

	protected override async Task _Setup(bool loadingData)
	{
		if (!_gridCreated)
			CreateHexGrid();

		EmitSignal(ManagerBase.SignalName.SetupCompleted);
		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		EmitSignal(SignalName.ExecuteCompleted);
		await Task.CompletedTask;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (!DebugMode) return;
		if (_cellArray == null) return;

		int newHash = DebugHighlightedCellIndices.Length > 0
			? DebugHighlightedCellIndices[0] ^ DebugHighlightedCellIndices.Length
			: 0;

		// Only rebuild mesh when the highlight actually changes
		if (newHash == _lastHighlightHash && _debugHighlightMesh != null)
			return;

		_lastHighlightHash = newHash;
		RebuildDebugHighlightMesh();
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>();

		if (_countryKeyByIndex == null || _countryKeyByIndex.Length == 0)
			return data;

		var keys = new long[_countryKeyByIndex.Length];
		for (int i = 0; i < _countryKeyByIndex.Length; i++)
			keys[i] = _countryKeyByIndex[i];

		data["country_keys"] = keys;
		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		_loadedCountryKeysFromSave = null;

		if (data == null || data.Count == 0)
			return;

		if (!data.TryGetValue("country_keys", out var v))
			return;

		if (v.VariantType == Variant.Type.Nil)
			return;

		if (v.VariantType == Variant.Type.PackedInt64Array)
		{
			var arr = v.AsInt64Array();
			_loadedCountryKeysFromSave = arr ?? Array.Empty<long>();
			return;
		}

		if (v.VariantType == Variant.Type.PackedInt32Array)
		{
			var ints = v.AsInt32Array();
			if (ints == null)
			{
				_loadedCountryKeysFromSave = Array.Empty<long>();
				return;
			}

			_loadedCountryKeysFromSave = new long[ints.Length];
			for (int i = 0; i < ints.Length; i++)
				_loadedCountryKeysFromSave[i] = ints[i];

			return;
		}

		if (v.VariantType == Variant.Type.PackedByteArray)
		{
			var bytes = v.AsByteArray();
			if (bytes == null || bytes.Length == 0 || (bytes.Length % 8) != 0)
				return;

			int n = bytes.Length / 8;
			_loadedCountryKeysFromSave = new long[n];

			for (int i = 0; i < n; i++)
				_loadedCountryKeysFromSave[i] = BitConverter.ToInt64(bytes, i * 8);
		}
	}

	public void CreateHexGrid()
	{
		_gridCreated = true;

		List<Vector3> verts = new();
		List<int[]> faces = new();

		InitializeIcosahedron(verts, faces);

		for (int r = 0; r < resolution; r++)
			Subdivide(verts, faces);

		List<int>[] vertexToFaces = new List<int>[verts.Count];
		for (int i = 0; i < verts.Count; i++)
			vertexToFaces[i] = new List<int>();

		for (int i = 0; i < faces.Count; i++)
		{
			foreach (int vIdx in faces[i])
				vertexToFaces[vIdx].Add(i);
		}

		_cellArray = new HexCellData[verts.Count];
		_countryKeyByIndex = new uint[verts.Count];
		_spatialHash.Clear();
		_hashSize = (Radius * 2.5f) / (Mathf.Pow(2, resolution) + 1);

		TryGetTextureRgba8Bytes("water", out var waterBytes, out var waterSize);
		TryGetTextureRgba8Bytes("countries", out var countryBytes, out var countrySize);

		// Cache per-texture offsets before entering the parallel loop
		Vector2 waterOffset = GetTextureOffset("water");
		Vector2 countryOffset = GetTextureOffset("countries");

		bool hasLoadedKeys =
			_loadedCountryKeysFromSave != null
			&& _loadedCountryKeysFromSave.Length == verts.Count;

		Parallel.For(0, verts.Count, i =>
		{
			Vector3 centerUnit = verts[i];
			Vector3 centerWorld = centerUnit * Radius;
			Vector2 latLon = Vector3ToLatLon(centerUnit);

			var corners = vertexToFaces[i]
				.Select(fIdx => GetTriangleCentroid(faces[fIdx], verts) * Radius)
				.ToArray();

			SortCorners(centerWorld, corners);

			Enums.HexGridType type = Enums.HexGridType.Land;
			if (waterBytes != null)
			{
				var uv = LatLonToUv(latLon, waterOffset);
				int px = Mathf.Clamp(
					(int)(uv.X * waterSize.X), 0, waterSize.X - 1
				);
				int py = Mathf.Clamp(
					(int)(uv.Y * waterSize.Y), 0, waterSize.Y - 1
				);

				byte a = GetRgba8A(waterBytes, waterSize.X, px, py);
				if (a > 26)
					type = Enums.HexGridType.Water;
			}

			uint countryKey = 0;
			if (hasLoadedKeys)
			{
				countryKey = (uint)_loadedCountryKeysFromSave![i];
			}
			else if (countryBytes != null)
			{
				var uv = LatLonToUv(latLon, countryOffset);
				int px = Mathf.Clamp(
					(int)(uv.X * countrySize.X), 0, countrySize.X - 1
				);
				int py = Mathf.Clamp(
					(int)(uv.Y * countrySize.Y), 0, countrySize.Y - 1
				);

				countryKey = GetCountryKeyFromRgba8(
					countryBytes, countrySize.X, px, py
				);
			}

			HashSet<int> neighbors = new HashSet<int>();
			foreach (int fIdx in vertexToFaces[i])
			{
				foreach (int vIdx in faces[fIdx])
				{
					if (vIdx != i)
						neighbors.Add(vIdx);
				}
			}

			_cellArray[i] = new HexCellData
			{
				LatLon = latLon,
				Center = centerWorld,
				Corners = corners,
				Index = i,
				cellType = type,
				IsPentagon = vertexToFaces[i].Count == 5,
				Neighbors = neighbors.ToArray()
			};

			_countryKeyByIndex[i] = countryKey;

			Vector3I hashKey = GetHashKey(centerWorld);
			_spatialHash.GetOrAdd(hashKey, _ => new ConcurrentBag<int>()).Add(i);
		});

		Cells.Clear();
		foreach (var cell in _cellArray)
			Cells[SnapVector2(cell.LatLon)] = cell;

		RebuildCountryIndex();

		_loadedCountryKeysFromSave = null;
		DebugHighlightedCellIndices = Array.Empty<int>();

		if (DebugMode)
		{
			int assigned = 0;
			for (int i = 0; i < _countryKeyByIndex.Length; i++)
			{
				if (_countryKeyByIndex[i] != 0)
					assigned++;
			}

			GD.Print(
				$"Country atlas: {assigned}/{_countryKeyByIndex.Length} cells assigned, "
				+ $"{_countryToCellIndices.Count} unique countries."
			);
		}
	}


	private void RebuildDebugHighlightMesh()
	{
		if (_debugHighlightMesh == null)
		{
			_debugHighlightMesh = new MeshInstance3D();
			AddChild(_debugHighlightMesh);

			var mat = new StandardMaterial3D();
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.AlbedoColor = new Color(1.0f, 0.3f, 0.1f, 0.45f);
			mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			// Slight offset outward so it renders above the globe surface
			mat.NoDepthTest = false;
			_debugHighlightMesh.MaterialOverride = mat;
		}

		if (DebugHighlightedCellIndices.Length == 0)
		{
			_debugHighlightMesh.Mesh = null;
			return;
		}

		var mesh = new ImmediateMesh();

		// Offset factor to push highlight slightly above the globe surface
		float lift = 1.002f;

		mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		foreach (int cellIdx in DebugHighlightedCellIndices)
		{
			var cell = _cellArray[cellIdx];
			var corners = cell.Corners;
			if (corners == null || corners.Length < 3) continue;

			Vector3 center = cell.Center * lift;

			// Fan triangulation from center through each consecutive corner pair
			for (int c = 0; c < corners.Length; c++)
			{
				int next = (c + 1) % corners.Length;

				mesh.SurfaceAddVertex(center);
				mesh.SurfaceAddVertex(corners[c] * lift);
				mesh.SurfaceAddVertex(corners[next] * lift);
			}
		}

		mesh.SurfaceEnd();
		_debugHighlightMesh.Mesh = mesh;
	}


	private void RebuildCountryIndex()
	{
		var tmp = new Dictionary<uint, List<int>>();

		for (int i = 0; i < _countryKeyByIndex.Length; i++)
		{
			uint key = _countryKeyByIndex[i];
			if (key == 0)
				continue;

			if (!tmp.TryGetValue(key, out var list))
			{
				list = new List<int>();
				tmp[key] = list;
			}

			list.Add(i);
		}

		_countryToCellIndices = new Dictionary<uint, int[]>(tmp.Count);
		foreach (var kvp in tmp)
			_countryToCellIndices[kvp.Key] = kvp.Value.ToArray();
	}

	public uint GetCountryKeyForIndex(int index)
	{
		if (_countryKeyByIndex == null || index < 0 || index >= _countryKeyByIndex.Length)
			return 0;

		return _countryKeyByIndex[index];
	}

	public int[] GetCountryCellIndicesForIndex(int index)
	{
		uint key = GetCountryKeyForIndex(index);
		if (key == 0)
			return Array.Empty<int>();

		return _countryToCellIndices.TryGetValue(key, out var indices)
			? indices
			: Array.Empty<int>();
	}

	public void SetDebugHighlightedCountryFromIndex(int index)
	{
		DebugHighlightedCellIndices = GetCountryCellIndicesForIndex(index);
	}


	private bool TryGetTextureRgba8Bytes(
		string textureKey,
		out byte[]? bytes,
		out Vector2I size
	)
	{
		bytes = null;
		size = Vector2I.Zero;

		Texture2D tex = null;

		if (textures != null)
		{
			if (textures.ContainsKey(textureKey))
			{
				tex = textures[textureKey];
			}
			else
			{
				foreach (var kv in textures)
				{
					if (string.Equals(
						    kv.Key,
						    textureKey,
						    StringComparison.OrdinalIgnoreCase
					    ))
					{
						tex = kv.Value;
						break;
					}
				}
			}
		}

		if (tex == null)
		{
			if (DebugMode)
				GD.PrintErr($"Texture key '{textureKey}' not found (or null).");
			return false;
		}

		var img = tex.GetImage();
		if (img == null)
		{
			if (DebugMode)
				GD.PrintErr(
					$"Texture '{textureKey}' returned null Image from GetImage(). " +
					$"If this is a CompressedTexture2D, enable import settings that " +
					$"allow CPU readback (keep original / no VRAM-only compression)."
				);
			return false;
		}

		if (img.GetFormat() != Image.Format.Rgba8)
			img.Convert(Image.Format.Rgba8);

		size = img.GetSize();
		var packed = img.GetData();
		if (packed.IsEmpty())
		{
			if (DebugMode)
				GD.PrintErr($"Texture '{textureKey}' Image.GetData() was empty.");
			return false;
		}

		bytes = packed.ToArray();
		return bytes.Length >= size.X * size.Y * 4;
	}

	private static Vector2 LatLonToUv(Vector2 latLon)
	{
		float u = (latLon.Y + 180f) / 360f;
		float v = 1.0f - ((latLon.X + 90f) / 180f);
		return new Vector2(u, v);
	}

	private static byte GetRgba8A(byte[] rgba, int width, int x, int y)
	{
		int idx = ((y * width) + x) * 4;
		return rgba[idx + 3];
	}

	private static uint GetCountryKeyFromRgba8(byte[] rgba, int width, int x, int y)
	{
		int idx = ((y * width) + x) * 4;

		byte r = rgba[idx + 0];
		byte g = rgba[idx + 1];
		byte b = rgba[idx + 2];
		byte a = rgba[idx + 3];

		if (a <= 26)
			return 0;

		if (r == 0 && g == 0 && b == 0)
			return 0;

		return (uint)(r | (g << 8) | (b << 16));
	}

	public HexCellData? GetCellFromLatLon(Vector2 coordinates)
	{
		Vector2 snapped = SnapVector2(coordinates);
		if (Cells.TryGetValue(snapped, out var cell)) return cell;

		// If exact key fails, fallback to position lookup
		return GetCellFromPosition(LatLonToVector3(coordinates));
	}

	public HexCellData? GetCellFromPosition(Vector3 worldPosition)
	{
		// Project position onto the sphere surface at the defined radius
		Vector3 surfacePoint = worldPosition.Normalized() * Radius;
		Vector3I hashKey = GetHashKey(surfacePoint);

		int bestIdx = -1;
		float maxDot = -1.0f;
		Vector3 normTarget = worldPosition.Normalized();

		// Check the voxel and its 26 neighbors
		for (int x = -1; x <= 1; x++)
			for (int y = -1; y <= 1; y++)
				for (int z = -1; z <= 1; z++)
				{
					if (_spatialHash.TryGetValue(hashKey + new Vector3I(x, y, z), out var bucket))
					{
						foreach (int idx in bucket)
						{
							float dot = normTarget.Dot(_cellArray[idx].Center.Normalized());
							if (dot > maxDot)
							{
								maxDot = dot;
								bestIdx = idx;
							}
						}
					}
				}

		return bestIdx != -1 ? _cellArray[bestIdx] : null;
	}


	public HexCellData? GetCellFromIndex(int index)
	{
		if (index < 0 || index >= _cellArray.Length) return null;
		return _cellArray[index];
	}

	#region Helper Methods

	private void InitializeIcosahedron(List<Vector3> verts, List<int[]> faces)
	{
		float phi = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
		verts.AddRange(new[]
		{
			new Vector3(-1, phi, 0).Normalized(), new Vector3(1, phi, 0).Normalized(),
			new Vector3(-1, -phi, 0).Normalized(), new Vector3(1, -phi, 0).Normalized(),
			new Vector3(0, -1, phi).Normalized(), new Vector3(0, 1, phi).Normalized(),
			new Vector3(0, -1, -phi).Normalized(), new Vector3(0, 1, -phi).Normalized(),
			new Vector3(phi, 0, -1).Normalized(), new Vector3(phi, 0, 1).Normalized(),
			new Vector3(-phi, 0, -1).Normalized(), new Vector3(-phi, 0, 1).Normalized()
		});
		int[] indices =
		{
			0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4,
			3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
		};
		for (int i = 0; i < indices.Length; i += 3) faces.Add(new[] { indices[i], indices[i + 1], indices[i + 2] });
	}

	private void Subdivide(List<Vector3> verts, List<int[]> faces)
	{
		List<int[]> nextFaces = new();
		Godot.Collections.Dictionary<long, int> midpoints = new();

		foreach (var f in faces)
		{
			int a = GetMidpoint(f[0], f[1], verts, midpoints);
			int b = GetMidpoint(f[1], f[2], verts, midpoints);
			int c = GetMidpoint(f[2], f[0], verts, midpoints);

			nextFaces.Add(new[] { f[0], a, c });
			nextFaces.Add(new[] { f[1], b, a });
			nextFaces.Add(new[] { f[2], c, b });
			nextFaces.Add(new[] { a, b, c });
		}

		faces.Clear();
		faces.AddRange(nextFaces);
	}

	private int GetMidpoint(int v1, int v2, List<Vector3> verts, Godot.Collections.Dictionary<long, int> cache)
	{
		long key = ((long)Math.Min(v1, v2) << 32) | (long)Math.Max(v1, v2);
		if (cache.TryGetValue(key, out int idx)) return idx;

		Vector3 edgeMid = ((verts[v1] + verts[v2]) * 0.5f).Normalized();
		verts.Add(edgeMid);
		int newIdx = verts.Count - 1;
		cache[key] = newIdx;
		return newIdx;
	}

	private void SortCorners(Vector3 center, Vector3[] corners)
	{
		Vector3 tangent = center.Cross(Vector3.Up).LengthSquared() < 0.01f
			? center.Cross(Vector3.Forward)
			: center.Cross(Vector3.Up);
		Vector3 bitangent = center.Cross(tangent);

		Array.Sort(corners, (a, b) =>
		{
			Vector3 dirA = (a - center).Normalized();
			Vector3 dirB = (b - center).Normalized();
			float angleA = Mathf.Atan2(dirA.Dot(tangent), dirA.Dot(bitangent));
			float angleB = Mathf.Atan2(dirB.Dot(tangent), dirB.Dot(bitangent));
			return angleA.CompareTo(angleB);
		});
	}

	private Vector3 GetTriangleCentroid(int[] f, List<Vector3> verts)
		=> ((verts[f[0]] + verts[f[1]] + verts[f[2]]) / 3.0f).Normalized();

	private Vector3I GetHashKey(Vector3 pos)
		=> new Vector3I(Mathf.FloorToInt(pos.X / _hashSize), Mathf.FloorToInt(pos.Y / _hashSize),
			Mathf.FloorToInt(pos.Z / _hashSize));

	private Vector2 SnapVector2(Vector2 v)
		=> new Vector2(Mathf.Snapped(v.X, 0.001f), Mathf.Snapped(v.Y, 0.001f));


	private Vector2 Vector3ToLatLon(Vector3 v)
	{
		// 1. Calculate base Lat/Lon from the 3D unit vector
		float lat = Mathf.RadToDeg(Mathf.Asin(v.Y));
		float lon = Mathf.RadToDeg(Mathf.Atan2(v.X, v.Z));

		// 2. Apply Offset
		lat += latLonOffset.X;
		lon += latLonOffset.Y;

		// 3. Wrap Longitude to maintain -180 to 180 range
		// Mathf.PosMod ensures the result is always positive before we shift it back
		lon = Mathf.PosMod(lon + 180f, 360f) - 180f;

		// 4. Clamp Latitude to -90 to 90 (standard globe bounds)
		lat = Mathf.Clamp(lat, -90f, 90f);

		return new Vector2(lat, lon);
	}

	private Vector3 LatLonToVector3(Vector2 coords)
	{
		// Inverse the offset to find the original 3D position on the sphere
		float lat = Mathf.DegToRad(coords.X - latLonOffset.X);
		float lon = Mathf.DegToRad(coords.Y - latLonOffset.Y);

		return new Vector3(
			Mathf.Cos(lat) * Mathf.Sin(lon),
			Mathf.Sin(lat),
			Mathf.Cos(lat) * Mathf.Cos(lon)
		).Normalized() * Radius;
	}


	public int GetTotalCells() => _cellArray.Length;

	#endregion


	public List<HexCellData> GetCellsInStepRange(HexCellData startCell, int steps)
	{
		List<HexCellData> results = new();

		if (steps == 0)
		{
			results.Add(startCell);
			return results;
		}

		// BFS Setup
		HashSet<int> visited = new() { startCell.Index };
		Queue<(int index, int distance)> queue = new();

		queue.Enqueue((startCell.Index, 0));
		results.Add(startCell); // Include origin? Remove if exclusive.

		while (queue.Count > 0)
		{
			var (currentIndex, currentDist) = queue.Dequeue();

			// If we are at the limit, don't add neighbors
			if (currentDist >= steps) continue;

			HexCellData current = _cellArray[currentIndex];

			foreach (int neighborIndex in current.Neighbors)
			{
				// If we haven't visited this neighbor yet
				if (visited.Add(neighborIndex))
				{
					var neighbor = _cellArray[neighborIndex];
					results.Add(neighbor);
					queue.Enqueue((neighborIndex, currentDist + 1));
				}
			}
		}

		return results;
	}

	private static Vector2 LatLonToUv(Vector2 latLon, Vector2 texOffset)
	{
		float lat = latLon.X + texOffset.X;
		float lon = latLon.Y + texOffset.Y;

		lon = Mathf.PosMod(lon + 180f, 360f) - 180f;
		lat = Mathf.Clamp(lat, -90f, 90f);

		float u = (lon + 180f) / 360f;
		float v = 1.0f - ((lat + 90f) / 180f);
		return new Vector2(u, v);
	}

	public List<HexCellData> GetCellsInWorldRadius(Vector3 origin, float worldRadius)
	{
		List<HexCellData> results = new();
		float radiusSqr = worldRadius * worldRadius;

		// Linear scan is extremely fast for arrays < 50k items.
		// Since _cellArray is a flat array, memory access is sequential and cache-friendly.
		for (int i = 0; i < _cellArray.Length; i++)
		{
			if (_cellArray[i].Center.DistanceSquaredTo(origin) <= radiusSqr)
			{
				results.Add(_cellArray[i]);
			}
		}

		return results;
	}

	private Vector2 GetTextureOffset(string textureKey)
	{
		if (textureOffsets != null && textureOffsets.TryGetValue(textureKey, out var offset))
			return offset;

		return Vector2.Zero;
	}


	public override void _Input(InputEvent @event)
	{
		if (DebugMode)
		{
			base._Input(@event);
			if (@event.IsPressed())
			{
				InputManager inputManager = InputManager.Instance;

				if (inputManager == null) return;

				HexCellData? currentCell = inputManager.CurrentCell;

				if (currentCell == null) return;
			}
		}
	}

	public override void Deinitialize()
	{
		return;
	}
}