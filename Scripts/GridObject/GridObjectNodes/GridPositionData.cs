using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GridPositionData : Node3D
{
    [Export] GridObject parentGridObject;
    [Export] public int gridHeight;
    [Export] GridShape gridShape;
    
    // [Y][X, Z] where Y=height levels, X=East-West, Z=North-South
    public GridCell[][,] gridCells { get; protected set; }

    public GridCell GridCell
    {
        get
        {
            GridSystem.Instance.TyGetGridCellFromWorldPosition(
                this.GlobalPosition, 
                out GridCell cell, 
                false
            );
            return cell;
        }
    }

    public Enums.Direction Direction { get; private set; }
    
    [Signal]
    public delegate void GridPositionDataUpdatedEventHandler(GridPositionData gridPositionData);
    
    [Signal]
    public delegate void GridCellPositionUpdatedEventHandler(Vector3I newGridCoordinates);

    #region Functions

    public GridPositionData()
    {
        this.gridShape = new GridShape();
        this.gridHeight = 1;
    }
    
    public GridPositionData(int gridHeight, GridShape gridShape)
    {
        this.gridShape = gridShape;
        this.gridHeight = gridHeight;
    }

 public void SetGridCell(GridCell gridCell)
{
    // Clear previous occupancy
    if (gridCells != null)
    {
        for (int y = 0; y < gridCells.Length; y++)
        {
            for (int x = 0; x < gridCells[y].GetLength(0); x++)
            {
                for (int z = 0; z < gridCells[y].GetLength(1); z++)
                {
                    var cell = gridCells[y][x, z];
                    if (cell == null) continue;

                    cell.RestoreOriginalState();
                    cell.RemoveGridObject(parentGridObject, cell.originalState, rebuildConnections: false);

                    gridCells[y][x, z] = null;
                }
            }
        }
    }

    if (gridCell == null)
    {
        GD.Print("gridcell is null, returning");
        return;
    }

    // Ensure backing array is initialized
    if (gridCells == null || gridCells.Length != gridHeight)
    {
        gridCells = new GridCell[gridHeight][,];
        for (int y = 0; y < gridHeight; y++)
            gridCells[y] = new GridCell[gridShape.GridSizeX, gridShape.GridSizeZ];
    }

    bool isWalkThrough = parentGridObject.gridObjectSettings.HasFlag(Enums.GridObjectSettings.CanWalkThrough);

    for (int y = 0; y < gridHeight; y++)
    {
        for (int x = 0; x < gridShape.GridSizeX; x++)
        {
            for (int z = 0; z < gridShape.GridSizeZ; z++)
            {
                if (!gridShape.GetGridShapeCell(x, z)) continue;

                int offsetX = x - gridShape.RootCellCoordinates.X;
                int offsetZ = z - gridShape.RootCellCoordinates.Y;
                var offset = new Vector3I(offsetX, y, offsetZ);

                var cellPos = gridCell.gridCoordinates + offset;
                var tempGridCell = GridSystem.Instance.GetGridCell(cellPos);
                if (tempGridCell == null) continue;

                gridCells[y][x, z] = tempGridCell;

                bool isRootCell = (y == 0 &&
                                   x == gridShape.RootCellCoordinates.X &&
                                   z == gridShape.RootCellCoordinates.Y);

                var tempNewState = tempGridCell.state;

                if (isWalkThrough)
                {
                    tempNewState &= ~Enums.GridCellState.Obstructed;
                }
                else
                {
                    tempNewState &= ~Enums.GridCellState.Empty;
                }

                if (!isRootCell)
                {
                    tempNewState &= ~Enums.GridCellState.Empty;
                }

                tempGridCell.AddGridObject(parentGridObject, tempNewState, rebuildConnections: !isWalkThrough);
            }
        }
    }

    EmitSignal("GridPositionDataUpdated", this);
    EmitSignal(SignalName.GridCellPositionUpdated, gridCell.gridCoordinates);
}

    public void SetDirection(Enums.Direction direction)
    {
        this.Direction = direction;
    }
    
    #endregion

    public void Setup()
    {
        gridCells = new GridCell[gridHeight][,];
        for (int y = 0; y < gridCells.Length; y++)
        {
            // UPDATED: Use GridSizeX and GridSizeZ instead of GridWidth and GridHeight
            gridCells[y] = new GridCell[gridShape.GridSizeX, gridShape.GridSizeZ];
        }
    }

    public void DebugShapePlacement(GridCell rootCell)
    {
        GD.Print($"=== Shape Placement Debug ===");
        GD.Print($"Root Cell Coords: {rootCell.gridCoordinates}");
        GD.Print($"Root in Shape: {gridShape.RootCellCoordinates}");
        GD.Print($"Shape Size: {gridShape.GridSizeX}x{gridShape.GridSizeZ}");

        for (int z = 0; z < gridShape.GridSizeZ; z++)
        {
            string row = $"Row Z={z}: ";
            for (int x = 0; x < gridShape.GridSizeX; x++)
            {
                bool active = gridShape.GetGridShapeCell(x, z);
                bool isRoot = x == gridShape.RootCellCoordinates.X && 
                            z == gridShape.RootCellCoordinates.Y;

                if (isRoot) row += "[R]";
                else if (active) row += "[X]";
                else row += "[ ]";
            }
            GD.Print(row);
        }

        GD.Print("Grid cells occupied:");
        for (int z = 0; z < gridShape.GridSizeZ; z++)
        {
            for (int x = 0; x < gridShape.GridSizeX; x++)
            {
                if (!gridShape.GetGridShapeCell(x, z)) continue;

                var offsetX = x - gridShape.RootCellCoordinates.X;
                var offsetZ = z - gridShape.RootCellCoordinates.Y;
                var offset = new Vector3I(offsetX, 0, offsetZ);
                var finalCoords = rootCell.gridCoordinates + offset;

                GD.Print($"  Shape({x},{z}) -> Offset({offsetX},{offsetZ}) -> Grid{finalCoords}");
            }
        }
    }
}