using System;
using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class GridShape : Resource
{
    private int _gridWidth = 3;
    private int _gridHeight = 3;

    [Export]
    public int GridWidth
    {
        get => _gridWidth;
        set
        {
            if (_gridWidth == value || value <= 0)
                return;

            int oldWidth = _gridWidth;
            _gridWidth = value;

            RebuildShapeGrid(oldWidth, _gridHeight);
            if (Engine.IsEditorHint())
            {
                NotifyPropertyListChanged();
                EmitChanged();
            }
        }
    }

    [Export]
    public int GridHeight
    {
        get => _gridHeight;
        set
        {
            if (_gridHeight == value || value <= 0)
                return;

            int oldHeight = _gridHeight;
            _gridHeight = value;

            RebuildShapeGrid(_gridWidth, oldHeight);
            if (Engine.IsEditorHint())
            {
                NotifyPropertyListChanged();
                EmitChanged();
            }
        }
    }

    [Export]
    public Array<bool> ShapeGrid { get; private set; }

    public GridShape()
    {
        int newSize = _gridWidth * _gridHeight;
        ShapeGrid = new Array<bool>();
        ShapeGrid.Resize(newSize);
        ShapeGrid.Fill(false);
    }

    public void SetGridShapeCell(int x, int y, bool value)
    {
        if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight)
            return;

        int index = y * _gridWidth + x;
        if (index < ShapeGrid.Count)
        {
            if (ShapeGrid[index] == value)
                return;

            ShapeGrid[index] = value;
            if (Engine.IsEditorHint())
                EmitChanged();
        }
    }

    public bool GetGridShapeCell(int x, int y)
    {
        if (ShapeGrid == null)
            return false;

        if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight)
            return false;

        int index = y * _gridWidth + x;
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

    public void OnGridSizeChanged(int oldWidth, int oldHeight)
    {
        RebuildShapeGrid(oldWidth, oldHeight);
    }

    private void RebuildShapeGrid(int oldW, int oldH, bool force = false)
    {
        int newSize = _gridWidth * _gridHeight;
        var newGrid = new Array<bool>();
        newGrid.Resize(newSize);
        newGrid.Fill(false);

        if (!force && ShapeGrid != null && ShapeGrid.Count > 0 && oldW > 0 && oldH > 0)
        {
            int minW = Math.Min(oldW, _gridWidth);
            int minH = Math.Min(oldH, _gridHeight);
            for (int y = 0; y < minH; y++)
            {
                for (int x = 0; x < minW; x++)
                {
                    int oldIdx = y * oldW + x;
                    int newIdx = y * _gridWidth + x;
                    if (oldIdx < ShapeGrid.Count)
                        newGrid[newIdx] = ShapeGrid[oldIdx];
                }
            }
        }

        ShapeGrid = newGrid;
    }
}