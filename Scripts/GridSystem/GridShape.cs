using System;
using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class GridShape : Resource
{
    private int _gridSizeX = 3; 
    private int _gridSizeY = 3; 
    private int _gridSizeZ = 3; 

    [Export]
    public int GridSizeX
    {
        get => _gridSizeX;
        set
        {
            if (_gridSizeX == value || value <= 0) return;
            int oldSize = _gridSizeX;
            _gridSizeX = value;
            RebuildShapeGrid(oldSize, _gridSizeY, _gridSizeZ);
            NotifyChange();
        }
    }
    
    [Export]
    public int GridSizeY
    {
        get => _gridSizeY;
        set
        {
            if (_gridSizeY == value || value <= 0) return;
            int oldSize = _gridSizeY;
            _gridSizeY = value;
            RebuildShapeGrid(_gridSizeX, oldSize, _gridSizeZ);
            NotifyChange();
        }
    }

    [Export]
    public int GridSizeZ
    {
        get => _gridSizeZ;
        set
        {
            if (_gridSizeZ == value || value <= 0) return;
            int oldSize = _gridSizeZ;
            _gridSizeZ = value;
            RebuildShapeGrid(_gridSizeX, _gridSizeY, oldSize);
            NotifyChange();
        }
    }

    [Export] public Vector2I RootCellCoordinates { get; set; }

    [Export]
    public Array<bool> ShapeGrid { get; set; }

    public GridShape()
    {
        // Ensure we initialize with a safe default
        int newSize = _gridSizeX * _gridSizeY * _gridSizeZ;
        ShapeGrid = new Array<bool>();
        ShapeGrid.Resize(newSize);
        ShapeGrid.Fill(false);
    }

    // UPDATED: Now takes X, Y, and Z
    public void SetGridShapeCell(int x, int y, int z, bool value)
    {
        if (!IsValidCoordinate(x, y, z)) return;

        // Flat index: x + (z * width) + (y * width * depth)
        int index = x + (z * _gridSizeX) + (y * _gridSizeX * _gridSizeZ);

        if (index < ShapeGrid.Count)
        {
            if (ShapeGrid[index] == value) return;
            ShapeGrid[index] = value;
            
            if (Engine.IsEditorHint()) EmitChanged();
        }
    }

    // UPDATED: Now takes X, Y, and Z
    public bool GetGridShapeCell(int x, int y, int z)
    {
        if (ShapeGrid == null || !IsValidCoordinate(x, y, z)) return false;

        int index = x + (z * _gridSizeX) + (y * _gridSizeX * _gridSizeZ);
        return index < ShapeGrid.Count ? ShapeGrid[index] : false;
    }

    public void SetAllCells(bool value)
    {
        if (ShapeGrid == null || ShapeGrid.Count == 0) return;

        bool anyChange = false;
        for (int i = 0; i < ShapeGrid.Count; i++)
        {
            if (ShapeGrid[i] != value)
            {
                ShapeGrid[i] = value;
                anyChange = true;
            }
        }

        if (anyChange && Engine.IsEditorHint()) EmitChanged();
    }

    private bool IsValidCoordinate(int x, int y, int z)
    {
        return x >= 0 && x < _gridSizeX &&
               y >= 0 && y < _gridSizeY &&
               z >= 0 && _gridSizeZ > 0 && z < _gridSizeZ;
    }

    private void NotifyChange()
    {
        if (Engine.IsEditorHint())
        {
            NotifyPropertyListChanged();
            EmitChanged();
        }
    }

    private void RebuildShapeGrid(int oldX, int oldY, int oldZ, bool force = false)
    {
        int newSize = _gridSizeX * _gridSizeZ * _gridSizeY;
        var newGrid = new Array<bool>();
        newGrid.Resize(newSize);
        newGrid.Fill(false);

        if (!force && ShapeGrid != null && ShapeGrid.Count > 0 && oldX > 0 && oldY > 0 && oldZ > 0)
        {
            int minX = Math.Min(oldX, _gridSizeX);
            int minY = Math.Min(oldY, _gridSizeY);
            int minZ = Math.Min(oldZ, _gridSizeZ);

            for (int y = 0; y < minY; y++)
            {
                for (int z = 0; z < minZ; z++)
                {
                    for (int x = 0; x < minX; x++)
                    {
                        // Calculate OLD index
                        int oldIdx = x + (z * oldX) + (y * oldX * oldZ);
                        // Calculate NEW index
                        int newIdx = x + (z * _gridSizeX) + (y * _gridSizeX * _gridSizeZ);

                        if (oldIdx < ShapeGrid.Count && newIdx < newSize)
                        {
                            newGrid[newIdx] = ShapeGrid[oldIdx];
                        }
                    }
                }
            }
        }
        ShapeGrid = newGrid;
    }
}