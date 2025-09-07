using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GridPositionData : Node
{
    [Export] GridObject parentGridObject;
    [Export] int gridHeight;
    [Export] GridShape gridShape;
    private List<GridCell> gridCells = new List<GridCell>();


    public GridCell GridCell
    {
        get
        {
            if (gridCells.Count == 0) return null;
            else return gridCells[0];

        }
    }
    
    
    public Enums.Direction Direction {get; private set;}
    [Signal]
    public delegate void  GridPositionDataUpdatedEventHandler(GridPositionData gridPositionData);
    
    #region Functions

    public GridPositionData()
    {
        this.gridShape = new  GridShape();
        this.gridHeight = 1;
    }
    public GridPositionData(int gridHeight, GridShape gridShape)
    {
        this.gridShape = gridShape;
        this.gridHeight = gridHeight;
    }
    public void SetGridCell(GridCell gridCell)
    {
        // Clear previous grid cell references and restore original states
        foreach (var cell in gridCells)
        {
            cell.RestoreOriginalState();
        }

        gridCells.Clear();

        if (gridCell == null)
        {
            GD.Print("gridcell is null, returning");
            return;
        }

        GD.Print(gridCell.gridCoordinates);
        //Add the base cell
        gridCells.Add(gridCell);
        var newState = gridCell.state & ~Enums.GridCellState.Walkable;
        gridCell.SetGridObject(parentGridObject, newState);
        //Add additional cells based on shape and height
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridShape.GridWidth; x++)
            {
                for (int z = 0; z < gridShape.GridHeight; z++)
                {
                    if (x == 0 && y == 0 && z == 0) continue;

                    var offset = new Vector3I(x, y, z);
                    var cellPosition = gridCell.gridCoordinates + offset;
                    var tempGridCell = GridSystem.Instance.GetGridCell(cellPosition);

                    if (tempGridCell != null && ! gridCells.Contains(tempGridCell))
                    {
                        gridCells.Append(tempGridCell);
                        var tempNewState = tempGridCell.state & ~Enums.GridCellState.Empty;
                        tempGridCell.SetGridObject(parentGridObject, tempNewState);
                    }
                    EmitSignal("GridPositionDataUpdated", this);
                }
            }
        }

}

    public void SetDirection(Enums.Direction direction)
    {
	    this.Direction = direction;
    }
    #endregion
}