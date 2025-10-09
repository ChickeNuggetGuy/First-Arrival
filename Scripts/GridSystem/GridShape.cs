using System;
using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class GridShape : Resource
{
    // Use X and Z to match grid coordinates
    private int _gridSizeX = 3;  // East-West extent
    private int _gridSizeZ = 3;  // North-South extent (depth)

    [Export]
    public int GridSizeX
    {
        get => _gridSizeX;
        set
        {
            if (_gridSizeX == value || value <= 0)
                return;

            int oldSize = _gridSizeX;
            _gridSizeX = value;

            RebuildShapeGrid(oldSize, _gridSizeZ);
            if (Engine.IsEditorHint())
            {
                NotifyPropertyListChanged();
                EmitChanged();
            }
        }
    }

    [Export]
    public int GridSizeZ
    {
        get => _gridSizeZ;
        set
        {
            if (_gridSizeZ == value || value <= 0)
                return;

            int oldSize = _gridSizeZ;
            _gridSizeZ = value;

            RebuildShapeGrid(_gridSizeX, oldSize);
            if (Engine.IsEditorHint())
            {
                NotifyPropertyListChanged();
                EmitChanged();
            }
        }
    }

    // Root coordinates now clearly X and Z
    [Export] public Vector2I RootCellCoordinates { get; set; } // X, Z

    [Export]
    public Array<bool> ShapeGrid { get; private set; }

    public GridShape()
    {
        int newSize = _gridSizeX * _gridSizeZ;
        ShapeGrid = new Array<bool>();
        ShapeGrid.Resize(newSize);
        ShapeGrid.Fill(false);
    }

    // Now takes x and z instead of x and y
    public void SetGridShapeCell(int x, int z, bool value)
    {
        if (x < 0 || x >= _gridSizeX || z < 0 || z >= _gridSizeZ)
            return;

        int index = z * _gridSizeX + x;
        if (index < ShapeGrid.Count)
        {
            if (ShapeGrid[index] == value)
                return;

            ShapeGrid[index] = value;
            if (Engine.IsEditorHint())
                EmitChanged();
        }
    }

    public bool GetGridShapeCell(int x, int z)
    {
        if (ShapeGrid == null)
            return false;

        if (x < 0 || x >= _gridSizeX || z < 0 || z >= _gridSizeZ)
            return false;

        int index = z * _gridSizeX + x;
        return index < ShapeGrid.Count ? ShapeGrid[index] : false;
    }

    public void SetAllCells(bool value)
    {
        if (ShapeGrid == null || ShapeGrid.Count == 0)
            return;

        bool anyChange = false;
        for (int i = 0; i < ShapeGrid.Count; i++)
        {
            if (ShapeGrid[i] != value)
            {
                ShapeGrid[i] = value;
                anyChange = true;
            }
        }

        if (anyChange && Engine.IsEditorHint())
            EmitChanged();
    }

    public void OnGridSizeChanged(int oldSizeX, int oldSizeZ)
    {
        RebuildShapeGrid(oldSizeX, oldSizeZ);
    }

    private void RebuildShapeGrid(int oldX, int oldZ, bool force = false)
    {
        int newSize = _gridSizeX * _gridSizeZ;
        var newGrid = new Array<bool>();
        newGrid.Resize(newSize);
        newGrid.Fill(false);

        if (!force && ShapeGrid != null && ShapeGrid.Count > 0 && oldX > 0 && oldZ > 0)
        {
            int minX = Math.Min(oldX, _gridSizeX);
            int minZ = Math.Min(oldZ, _gridSizeZ);
            for (int z = 0; z < minZ; z++)
            {
                for (int x = 0; x < minX; x++)
                {
                    int oldIdx = z * oldX + x;
                    int newIdx = z * _gridSizeX + x;
                    if (oldIdx < ShapeGrid.Count)
                        newGrid[newIdx] = ShapeGrid[oldIdx];
                }
            }
        }

        ShapeGrid = newGrid;
    }
}