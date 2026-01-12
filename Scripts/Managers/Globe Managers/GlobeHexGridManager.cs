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
    
    [Export] private Godot.Collections.Dictionary<string, Texture2D> textures =  new Godot.Collections.Dictionary<string, Texture2D>();

    // Dictionary for LatLon lookup
    public System.Collections.Generic.Dictionary<Vector2, HexCellData> Cells = new();
    
    // Array for fast index-based access
    private HexCellData[] _cellArray;

    // Spatial Hash for O(1) world-position lookups
    private ConcurrentDictionary<Vector3I, ConcurrentBag<int>> _spatialHash = new();
    private float _hashSize;

    public override string GetManagerName() => "GlobeHexGridManager";

    protected override async Task _Setup(bool loadingData)
    {
        CreateHexGrid();
        EmitSignal(SignalName.SetupCompleted);
        await Task.CompletedTask;
    }

    protected override async Task _Execute(bool loadingData)
    {
        EmitSignal(SignalName.ExecuteCompleted);
        await Task.CompletedTask;
    }

    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        return new Godot.Collections.Dictionary<string, Variant>();
    }

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
    }

    public void CreateHexGrid()
{
    List<Vector3> verts = new();
    List<int[]> faces = new();

    InitializeIcosahedron(verts, faces);
    
    Image maskImage = null;
    Vector2I imageSize = Vector2I.Zero;

    if (textures.ContainsKey("water") && textures["water"] != null)
    {
        maskImage = textures["water"].GetImage();
        imageSize = maskImage.GetSize();
    }
    
    //Subdivide 
    for (int r = 0; r < resolution; r++)
    {
        Subdivide(verts, faces);
    }
	
    List<int>[] vertexToFaces = new List<int>[verts.Count];
    for (int i = 0; i < verts.Count; i++) vertexToFaces[i] = new List<int>();
    for (int i = 0; i < faces.Count; i++)
    {
        foreach (int vIdx in faces[i]) vertexToFaces[vIdx].Add(i);
    }
	
    _cellArray = new HexCellData[verts.Count];
    _spatialHash.Clear();
    _hashSize = (Radius * 2.5f) / (Mathf.Pow(2, resolution) + 1);
    
    
    Parallel.For(0, verts.Count, i =>
    {
        Vector3 centerUnit = verts[i];
        Vector3 centerWorld = centerUnit * Radius;
        Vector2 latLon = Vector3ToLatLon(centerUnit);

        var corners = vertexToFaces[i]
            .Select(fIdx => GetTriangleCentroid(faces[fIdx], verts) * Radius)
            .ToArray();

        SortCorners(centerWorld, corners);

        // Determine Cell Type
        Enums.HexGridType type = Enums.HexGridType.Land;
        if (maskImage != null)
        {
            float u = (latLon.Y + 180f) / 360f;
            float v = 1.0f - ((latLon.X + 90f) / 180f);

            int px = Mathf.Clamp((int)(u * imageSize.X), 0, imageSize.X - 1);
            int py = Mathf.Clamp((int)(v * imageSize.Y), 0, imageSize.Y - 1);

            if (maskImage.GetPixel(px, py).A > 0.1f) 
                type = Enums.HexGridType.Water;
        }
        
        HashSet<int> neighbors = new HashSet<int>();
        foreach (int fIdx in vertexToFaces[i])
        {
            foreach (int vIdx in faces[fIdx])
            {
                if (vIdx != i) neighbors.Add(vIdx);
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

        Vector3I hashKey = GetHashKey(centerWorld);
        _spatialHash.GetOrAdd(hashKey, _ => new ConcurrentBag<int>()).Add(i);
    });
	
    Cells.Clear();
    foreach (var cell in _cellArray)
    {
        Cells[SnapVector2(cell.LatLon)] = cell;
    }
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
        verts.AddRange(new[] {
            new Vector3(-1, phi, 0).Normalized(), new Vector3(1, phi, 0).Normalized(),
            new Vector3(-1, -phi, 0).Normalized(), new Vector3(1, -phi, 0).Normalized(),
            new Vector3(0, -1, phi).Normalized(), new Vector3(0, 1, phi).Normalized(),
            new Vector3(0, -1, -phi).Normalized(), new Vector3(0, 1, -phi).Normalized(),
            new Vector3(phi, 0, -1).Normalized(), new Vector3(phi, 0, 1).Normalized(),
            new Vector3(-phi, 0, -1).Normalized(), new Vector3(-phi, 0, 1).Normalized()
        });
        int[] indices = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        for (int i = 0; i < indices.Length; i += 3) faces.Add(new[] { indices[i], indices[i+1], indices[i+2] });
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
        Vector3 tangent = center.Cross(Vector3.Up).LengthSquared() < 0.01f ? center.Cross(Vector3.Forward) : center.Cross(Vector3.Up);
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
        => new Vector3I(Mathf.FloorToInt(pos.X / _hashSize), Mathf.FloorToInt(pos.Y / _hashSize), Mathf.FloorToInt(pos.Z / _hashSize));

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
    
    
    public override void _Input(InputEvent @event)
    {
	    if(DebugMode)
	    {
		    base._Input(@event);
		    if (@event.IsPressed())
		    {
			    GlobeInputManager inputManager = GlobeInputManager.Instance;

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