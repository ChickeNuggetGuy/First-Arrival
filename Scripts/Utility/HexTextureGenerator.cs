using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public partial class HexTextureGenerator : Node
{
    [ExportCategory("Setup")]
    [Export] private Node _gridManagerNode;
    [Export] private Texture2D _sourceTexture;
    
    [ExportCategory("Output Settings")]
    [Export] private bool _useSourceResolution = true;
    [Export] private Vector2I _customResolution = new Vector2I(1024, 512);
    [Export(PropertyHint.File, "*.png")] private string _outputPath = "res://Generated/hex_texture.png";
    
    [ExportCategory("Processing")]
    [Export(PropertyHint.Range, "0.0,1.0")] private float _alphaThreshold = 0.5f;

    [Export]
    public bool ProcessTexture
    {
        get => false;
        set { if (value) _ = GenerateHexTexture(); }
    }

    private struct TempCell
    {
        public int Index;
        public Vector3 Center;
    }
    
    // Stores: R_sum, G_sum, B_sum, Alpha_sum
    private struct CellAccumulator
    {
        public float R;
        public float G;
        public float B;
        public float A;
        public int Count;
    }
    
    private TempCell[] _tempCells;
    private ConcurrentDictionary<Vector3I, List<int>> _tempSpatialHash;
    private float _hashSize;
    private float _gridRadius;

    private async Task GenerateHexTexture()
    {
        if (_gridManagerNode == null || _sourceTexture == null)
        {
            GD.PrintErr("HexTextureGenerator: Please assign both Grid Manager and Source Texture.");
            return;
        }

        int resolution;
        try 
        {
            resolution = (int)_gridManagerNode.Get("resolution");
            _gridRadius = (float)_gridManagerNode.Get("radius");
        }
        catch (Exception)
        {
            GD.PrintErr("HexTextureGenerator: Assigned Node is missing 'resolution' or 'radius'.");
            return;
        }

        BuildTemporaryGrid(resolution, _gridRadius);

        var startTime = Time.GetTicksMsec();
        Image srcImage = _sourceTexture.GetImage();
        if (srcImage == null) return;

        if (srcImage.IsCompressed()) srcImage.Decompress();
        if (srcImage.GetFormat() != Image.Format.Rgba8) srcImage.Convert(Image.Format.Rgba8);

        int srcW = srcImage.GetWidth();
        int srcH = srcImage.GetHeight();
        byte[] srcData = srcImage.GetData();

        int outW = _useSourceResolution ? srcW : _customResolution.X;
        int outH = _useSourceResolution ? srcH : _customResolution.Y;

        GD.Print($"HexTextureGenerator: Sampling {srcW}x{srcH} | Outputting {outW}x{outH}...");

        // Thread-safe accumulator
        var cellData = new ConcurrentDictionary<int, CellAccumulator>();

        // 1. PASS 1: Sample Source Texture
        Parallel.For(0, srcH, y =>
        {
            for (int x = 0; x < srcW; x++)
            {
                // Standard Equirectangular UV to Sphere Mapping
                float v = y / (float)srcH;
                float u = x / (float)srcW;
                
                Vector3 worldPos = LatLonToVector3(90.0f - (v * 180.0f), (u * 360.0f) - 180.0f, _gridRadius);
                int cellIndex = GetClosestCellIndex(worldPos);

                if (cellIndex != -1)
                {
                    int bIdx = (y * srcW + x) * 4;
                    if (bIdx + 3 < srcData.Length)
                    {
                        float r = srcData[bIdx] / 255.0f;
                        float g = srcData[bIdx + 1] / 255.0f;
                        float b = srcData[bIdx + 2] / 255.0f;
                        float a = srcData[bIdx + 3] / 255.0f;

                        // Atomic Update
                        cellData.AddOrUpdate(cellIndex, 
                            new CellAccumulator { R = r * a, G = g * a, B = b * a, A = a, Count = 1 }, 
                            (_, old) => new CellAccumulator { 
                                R = old.R + (r * a), 
                                G = old.G + (g * a), 
                                B = old.B + (b * a), 
                                A = old.A + a, 
                                Count = old.Count + 1 
                            });
                    }
                }
            }
        });

        // 2. Resolve Colors (Apply Hard Edge Threshold)
        Dictionary<int, Color> finalHexColors = new();
        foreach (var kvp in cellData)
        {
            var acc = kvp.Value;
            if (acc.Count == 0) continue;

            // Average Alpha of the entire hexagon area
            float avgAlpha = acc.A / acc.Count;

            // HARD EDGE LOGIC:
            // If the average alpha is above threshold, the hex becomes Fully Opaque (1.0).
            // Otherwise, it becomes Fully Transparent (0.0).
            if (avgAlpha >= _alphaThreshold)
            {
                // Normalize color by dividing by Total Alpha (un-premultiply the average)
                // This ensures the color is the average of only the visible pixels.
                float div = acc.A > 0.0001f ? acc.A : 1.0f;
                finalHexColors[kvp.Key] = new Color(acc.R / div, acc.G / div, acc.B / div, 1.0f);
            }
            else
            {
                finalHexColors[kvp.Key] = Colors.Transparent;
            }
        }

        // 3. PASS 2: Generate Output Texture
        byte[] outData = new byte[outW * outH * 4];

        Parallel.For(0, outH, y =>
        {
            for (int x = 0; x < outW; x++)
            {
                float v = y / (float)outH;
                float u = x / (float)outW;
                
                Vector3 worldPos = LatLonToVector3(90.0f - (v * 180.0f), (u * 360.0f) - 180.0f, _gridRadius);
                int cellIndex = GetClosestCellIndex(worldPos);
                
                Color c = (cellIndex != -1 && finalHexColors.TryGetValue(cellIndex, out Color res)) ? res : Colors.Transparent;
                
                int b = (y * outW + x) * 4;
                outData[b] = (byte)(c.R * 255);
                outData[b+1] = (byte)(c.G * 255);
                outData[b+2] = (byte)(c.B * 255);
                outData[b+3] = (byte)(c.A * 255);
            }
        });

        // 4. Save
        Image resImg = Image.CreateFromData(outW, outH, false, Image.Format.Rgba8, outData);
        string dir = _outputPath.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dir)) DirAccess.MakeDirRecursiveAbsolute(dir);
        
        Error err = resImg.SavePng(_outputPath);
        if (err == Error.Ok)
        {
            GD.Print($"HexTextureGenerator: Success! Saved to {_outputPath} ({Time.GetTicksMsec() - startTime}ms)");
            if (Engine.IsEditorHint()) EditorInterface.Singleton.GetResourceFilesystem().Scan();
        }
        else
        {
            GD.PrintErr("HexTextureGenerator: Save failed: " + err);
        }

        _tempCells = null;
        _tempSpatialHash = null;
        await Task.CompletedTask;
    }

    private void BuildTemporaryGrid(int resolution, float radius)
    {
        List<Vector3> verts = new();
        List<int[]> faces = new();
        InitializeIcosahedron(verts, faces);
        for (int r = 0; r < resolution; r++) Subdivide(verts, faces);

        _tempCells = new TempCell[verts.Count];
        _tempSpatialHash = new ConcurrentDictionary<Vector3I, List<int>>();
        // Slightly increased hash size to prevent edge case lookups failing
        _hashSize = (radius * 2.5f) / (Mathf.Pow(2, resolution) + 1);

        Parallel.For(0, verts.Count, i =>
        {
            Vector3 center = verts[i] * radius;
            _tempCells[i] = new TempCell { Index = i, Center = center };
            Vector3I key = GetHashKey(center);
            _tempSpatialHash.GetOrAdd(key, _ => new List<int>()).Add(i);
        });
    }

    private int GetClosestCellIndex(Vector3 worldPos)
    {
        Vector3I key = GetHashKey(worldPos);
        int bestIdx = -1;
        float maxDot = -2.0f;
        Vector3 dir = worldPos.Normalized();

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            if (_tempSpatialHash.TryGetValue(key + new Vector3I(x,y,z), out var bucket))
            {
                foreach(int idx in bucket)
                {
                    float dot = dir.Dot(_tempCells[idx].Center.Normalized());
                    if (dot > maxDot) { maxDot = dot; bestIdx = idx; }
                }
            }
        }
        return bestIdx;
    }

    private Vector3I GetHashKey(Vector3 pos) => 
        new Vector3I(Mathf.FloorToInt(pos.X / _hashSize), Mathf.FloorToInt(pos.Y / _hashSize), Mathf.FloorToInt(pos.Z / _hashSize));

    private Vector3 LatLonToVector3(float latDeg, float lonDeg, float radius)
    {
        float lat = Mathf.DegToRad(latDeg);
        float lon = Mathf.DegToRad(lonDeg);
        return new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon)).Normalized() * radius;
    }

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
        Dictionary<long, int> midpoints = new();
        foreach (var f in faces)
        {
            int a = GetMidpoint(f[0], f[1], verts, midpoints);
            int b = GetMidpoint(f[1], f[2], verts, midpoints);
            int c = GetMidpoint(f[2], f[0], verts, midpoints);
            nextFaces.Add(new[] { f[0], a, c }); nextFaces.Add(new[] { f[1], b, a });
            nextFaces.Add(new[] { f[2], c, b }); nextFaces.Add(new[] { a, b, c });
        }
        faces.Clear(); faces.AddRange(nextFaces);
    }

    private int GetMidpoint(int v1, int v2, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = ((long)Math.Min(v1, v2) << 32) | (long)Math.Max(v1, v2);
        if (cache.TryGetValue(key, out int idx)) return idx;
        Vector3 edgeMid = ((verts[v1] + verts[v2]) * 0.5f).Normalized();
        verts.Add(edgeMid);
        int newIdx = verts.Count - 1;
        cache[key] = newIdx;
        return newIdx;
    }
}