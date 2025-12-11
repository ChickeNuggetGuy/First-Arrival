using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class GridPositionData : Node3D
{
    [Export] GridObject parentGridObject;
    [Export] public GridShape gridShape;
    [Export] bool debugMode = false;
    
    // [Y][X, Z]
    public GridCell[][,] gridCells { get; protected set; }

    // Constants from your GDScript implementation
    private readonly Vector3 _cellSize = new Vector3(1, 1, 1);
    private readonly Vector3 _gridOffset = new Vector3(0, 0.5f, 0);

    public GridCell GridCell
    {
        get
        {
            if (Engine.IsEditorHint() && (GridSystem.Instance == null || !IsInstanceValid(GridSystem.Instance)))
                return null;

            GridSystem.Instance.TryGetGridCellFromWorldPosition(
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
        if (this.gridShape == null)
        {
            this.gridShape = new GridShape();
            this.gridShape.GridSizeY = 1;
        }
    }
    
    public GridPositionData(int gridHeight, GridShape gridShape)
    {
        this.gridShape = gridShape;
        this.gridShape.GridSizeY = gridHeight;
    }

    public void SetGridCell(GridCell gridCell)
    {
        if (gridCells != null)
        {
            for (int y = 0; y < gridCells.Length; y++)
            {
                if (gridCells[y] == null) continue;

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

        if (gridCell == null) return;

        if (gridCells == null || 
            gridCells.Length != gridShape.GridSizeY || 
            (gridCells.Length > 0 && gridCells[0] != null && 
            (gridCells[0].GetLength(0) != gridShape.GridSizeX || gridCells[0].GetLength(1) != gridShape.GridSizeZ)))
        {
            Setup();
        }

        bool isWalkThrough = parentGridObject.gridObjectSettings.HasFlag(Enums.GridObjectSettings.CanWalkThrough);

        for (int y = 0; y < gridShape.GridSizeY; y++)
        {
            for (int x = 0; x < gridShape.GridSizeX; x++)
            {
                for (int z = 0; z < gridShape.GridSizeZ; z++)
                {
                    if (!gridShape.GetGridShapeCell(x, y, z)) continue;

                    int offsetX = x - gridShape.RootCellCoordinates.X;
                    int offsetZ = z - gridShape.RootCellCoordinates.Y;
                    var offset = new Vector3I(offsetX, y, offsetZ);

                    var targetCoords = gridCell.gridCoordinates + offset;
                    
                    if (GridSystem.Instance == null) continue;
                    var tempGridCell = GridSystem.Instance.GetGridCell(targetCoords);

                    if (tempGridCell == null) continue;

                    gridCells[y][x, z] = tempGridCell;

                    bool isRootCell = (y == 0 &&
                                       x == gridShape.RootCellCoordinates.X &&
                                       z == gridShape.RootCellCoordinates.Y);

                    var tempNewState = tempGridCell.state;

                    if (isWalkThrough)
                        tempNewState &= ~Enums.GridCellState.Obstructed;
                    else
                        tempNewState &= ~Enums.GridCellState.Empty;

                    if (!isRootCell)
                        tempNewState &= ~Enums.GridCellState.Empty;

                    tempGridCell.AddGridObject(parentGridObject, tempNewState, rebuildConnections: !isWalkThrough);
                }
            }
        }

        EmitSignal(SignalName.GridPositionDataUpdated, this);
        EmitSignal(SignalName.GridCellPositionUpdated, gridCell.gridCoordinates);
    }

    public void SetDirection(Enums.Direction direction)
    {
        this.Direction = direction;
    }
    
    #endregion

    public void Setup()
    {
	    if (parentGridObject == null)
	    {
		    parentGridObject = this.GetParent() is GridObject gridObject ? gridObject : null;
	    }
        gridCells = new GridCell[gridShape.GridSizeY][,];
        for (int y = 0; y < gridCells.Length; y++)
        {
            gridCells[y] = new GridCell[gridShape.GridSizeX, gridShape.GridSizeZ];
        }
    }

    public void DebugShapePlacement(GridCell rootCell)
    {
        // Debug method left as is for console logging if needed
    }

public override void _Process(double delta)
{
    base._Process(delta);

    if (!Engine.IsEditorHint() || !debugMode || gridShape == null) return;
	
    Vector3 localPos = GlobalPosition - _gridOffset;
    Vector3 searchPos = localPos - new Vector3(0, _cellSize.Y * 0.5f, 0);
    
    Vector3 gridIndicesRaw = new Vector3(
        searchPos.X / _cellSize.X,
        searchPos.Y / _cellSize.Y,
        -searchPos.Z / _cellSize.X  // NEGATE Z!
    );
    Vector3 gridOriginIndices = gridIndicesRaw.Round();

    int sizeX = gridShape.GridSizeX;
    int sizeY = gridShape.GridSizeY;
    int sizeZ = gridShape.GridSizeZ;
    Vector2I rootOffset = gridShape.RootCellCoordinates;

    for (int y = 0; y < sizeY; y++)
    {
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                int relX = x - rootOffset.X;
                int relZ = z - rootOffset.Y;
                int relY = y;

                Vector3 gridIndexOffset = new Vector3(relX, relY, relZ);
                Vector3 currentCellIndex = gridOriginIndices + gridIndexOffset;

                // Convert grid index back to world (with negative Z)
                Vector3 drawPos = new Vector3(
                    (currentCellIndex.X + 0.5f) * _cellSize.X,
                    (currentCellIndex.Y + 1.0f) * _cellSize.Y,
                    -(currentCellIndex.Z + 0.5f) * _cellSize.X  // NEGATIVE Z in world
                );

                bool isActive = gridShape.GetGridShapeCell(x, y, z);
                bool isRoot = (relX == 0 && relZ == 0 && relY == 0);

                Color color;
                if (isRoot) color = Colors.Green; 
                else if (isActive) color = Colors.Black; 
                else color = Colors.Gray;

                if (!isActive) color.A = 0.1f; 

                DebugDraw3D.DrawBox(drawPos, Quaternion.Identity, _cellSize, color, true);
            }
        }
    }
}
}