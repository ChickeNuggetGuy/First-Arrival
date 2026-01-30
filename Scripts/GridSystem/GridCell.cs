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
        new Vector3(-1, -1, -1),
        Enums.GridCellState.None,
        Enums.FogState.Visible,
        null
    );

    public Vector3I gridCoordinates { get; protected set; }
    public Vector3 worldCenter { get; protected set; }
    public Vector3 trueCenter { get; protected set; }
    
    public List<Vector3I> Connections => GridSystem.Instance?.GetConnections(gridCoordinates);

    public Enums.GridCellState originalState { get; protected set; }
    public Enums.GridCellState state { get; protected set; }
    public Enums.FogState fogState { get; protected set; }

    public Enums.UnitTeam UnitTeamSpawn { get; protected set; } = Enums.UnitTeam.All;

    public List<GridObject> gridObjects { get; protected set; }

    public InventoryGrid InventoryGrid { get; protected set; }
	
    public bool IsWalkable => state.HasFlag(Enums.GridCellState.Ground) && !state.HasFlag(Enums.GridCellState.Obstructed) && (GridSystem.Instance?.HasConnections(gridCoordinates) ?? false);

    public GridCell(Vector3I gridCoordinates, Vector3 worldCenter, Vector3 trueCenter, Enums.GridCellState state,
        Enums.FogState fogState, InventoryGrid inventory, Enums.UnitTeam unitTeamSpawn = Enums.UnitTeam.All
    )
    {
        this.gridCoordinates = gridCoordinates;
        this.worldCenter = worldCenter;
        this.trueCenter = trueCenter;
        this.state = state;
        this.originalState = state;
        SetFogState(fogState);
        this.gridObjects = new List<GridObject>();
        UnitTeamSpawn = unitTeamSpawn;
        if (inventory != null)
        {
	        InventoryGrid = inventory;
	        InventoryGrid.Initialize();
        }
    }

    public bool HasGridObject()
    {
        return (gridObjects != null && gridObjects.Count > 0);
    }

    public void SetFogState(Enums.FogState state)
    {
        this.fogState = state;
        if (HasGridObject())
        {
	        switch (this.fogState)
	        {
		        case Enums.FogState.Unseen:
			        foreach (var gridObject in gridObjects)
				        gridObject.Hide();
			        break;
		        case Enums.FogState.PreviouslySeen:
			        foreach (var gridObject in gridObjects)
				        gridObject.Show();
			        break;
		        case Enums.FogState.Visible:
			        foreach (var gridObject in gridObjects)
				        gridObject.Show();
			        break;
		        default:
			        throw new ArgumentOutOfRangeException();
	        }
        }
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


    public void SetInventory(InventoryGrid inventory)
    {
	    InventoryGrid = inventory;
	    InventoryGrid.Initialize();
    }
}