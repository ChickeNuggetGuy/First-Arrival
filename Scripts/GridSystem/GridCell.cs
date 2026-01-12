using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class GridCell
{
    public static GridCell Null = new GridCell(
        new Vector3I(-1, -1, -1),
        new Vector3(-1, -1, -1),
        Enums.GridCellState.None,
        Enums.FogState.Visible,
        null
    );

    public Vector3I gridCoordinates { get; protected set; }
    public Vector3 worldCenter { get; protected set; }

    public Vector3 worldPosition
    {
	    get
	    {
		    return worldCenter; 
	    }
    }

    // Removed local connections list - now queried from GridSystem
    public List<Vector3I> Connections => GridSystem.Instance?.GetConnections(gridCoordinates);

    public Enums.GridCellState originalState { get; protected set; }
    public Enums.GridCellState state { get; protected set; }
    public Enums.FogState fogState { get; protected set; }

    public Enums.UnitTeam UnitTeamSpawn { get; protected set; } = Enums.UnitTeam.All;

    public List<GridObject> gridObjects { get; protected set; }

    public InventoryGrid InventoryGrid { get; protected set; }

    // Now queries GridSystem for connections
    public bool IsWalkable => state.HasFlag(Enums.GridCellState.Ground) && !state.HasFlag(Enums.GridCellState.Obstructed) && (GridSystem.Instance?.HasConnections(gridCoordinates) ?? false);

    public GridCell(
        Vector3I gridCoordinates,
        Vector3 worldCenter,
        Enums.GridCellState state,
        Enums.FogState fogState,
        InventoryGrid inventory,
        Enums.UnitTeam unitTeamSpawn = Enums.UnitTeam.All
    )
    {
        this.gridCoordinates = gridCoordinates;
        this.worldCenter = worldCenter;
        this.state = state;
        this.originalState = state;
        this.fogState = fogState;
        this.gridObjects = new List<GridObject>();
        UnitTeamSpawn = unitTeamSpawn;
        InventoryGrid = inventory;
    }

    public bool HasGridObject()
    {
        return (gridObjects != null && gridObjects.Count > 0);
    }

    public void SetFogState(Enums.FogState state)
    {
        this.fogState = state;
    }

    public void SetUnitSpawnState(Enums.UnitTeam state)
    {
        this.UnitTeamSpawn = state;
    }

    public void SetWorldCenter(Vector3 worldCenter)
    {
        this.worldCenter = worldCenter;
    }

    public void SetState(Enums.GridCellState state)
    {
        if (this.state == state)
            return;

        this.state = state;

        GridSystem.Instance.UpdateGridCell(gridCoordinates);
        GridSystem.Instance.UpdateNeighborsConnections(this.gridCoordinates);
    }

    public void AddGridObject(GridObject gridObject, Enums.GridCellState newState, bool rebuildConnections)
    {
        this.gridObjects ??= new List<GridObject>();
        this.gridObjects.Add(gridObject);

        bool stateChanged = this.state != newState;
        if (stateChanged)
            this.state = newState;

        // Optionally rebuild connections if needed
        // if (rebuildConnections)
        // {
        //     GridSystem.Instance.UpdateGridCell(gridCoordinates);
        //     GridSystem.Instance.UpdateNeighborsConnections(this.gridCoordinates);
        // }
    }

    public void RemoveGridObject(GridObject gridObject, Enums.GridCellState newState, bool rebuildConnections)
    {
        if (gridObjects == null) return;
        if (!gridObjects.Contains(gridObject)) return;

        gridObjects.Remove(gridObject);

        bool stateChanged = this.state != newState;
        if (stateChanged)
            this.state = newState;

        if (rebuildConnections)
        {
            GridSystem.Instance.UpdateGridCell(gridCoordinates);
            GridSystem.Instance.UpdateNeighborsConnections(this.gridCoordinates);
        }
    }

    public void RemoveGridObject(GridObject gridObject, Enums.GridCellState newState)
    {
        if (!gridObjects.Contains(gridObject))
            return;

        this.gridObjects.Remove(gridObject);

        if (this.state != newState)
        {
            SetState(newState);
        }
        else
        {
            GridSystem.Instance.UpdateGridCell(gridCoordinates);
            GridSystem.Instance.UpdateNeighborsConnections(this.gridCoordinates);
        }
    }

    public void RestoreOriginalState()
    {
        state = originalState;
    }

    public void ModifyOriginalState(Enums.GridCellState newState)
    {
        originalState = newState;
    }
}