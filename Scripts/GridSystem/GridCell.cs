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

    public Vector3I GridCoordinates { get; protected set; }
    public Vector3 WorldCenter { get; protected set; }
    public Vector3 WorldPosition { get; protected set; }
    
    public List<Vector3I> Connections => GridSystem.Instance?.GetConnections(GridCoordinates);

    public Enums.GridCellState originalState { get; protected set; }
    public Enums.GridCellState state { get; protected set; }
    public Enums.FogState fogState { get; protected set; }

    public Enums.UnitTeam UnitTeamSpawn { get; protected set; } = Enums.UnitTeam.All;

    public List<GridObject> gridObjects { get; protected set; }

    public InventoryGrid InventoryGrid { get; protected set; }
	
    public bool IsWalkable => state.HasFlag(Enums.GridCellState.Ground) && !state.HasFlag(Enums.GridCellState.Obstructed) && (GridSystem.Instance?.HasConnections(GridCoordinates) ?? false);

    public GridCell(Vector3I gridCoordinates, Vector3 worldCenter, Vector3 worldPosition, Enums.GridCellState state,
        Enums.FogState fogState, InventoryGrid inventory, Enums.UnitTeam unitTeamSpawn = Enums.UnitTeam.All
    )
    {
        this.GridCoordinates = gridCoordinates;
        this.WorldCenter = worldCenter;
        this.WorldPosition = worldPosition;
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
       UpdateGridObjectVisibility();
    }

    public void UpdateGridObjectVisibility()
    {
	    if (HasGridObject())
	    {
		    switch (this.fogState)
		    {
			    case Enums.FogState.Unseen:
				    foreach (var gridObject in gridObjects)
				    {
					    if (gridObject.scenery || gridObject.Team == Enums.UnitTeam.Player)
					    {
						    continue;
					    }

					    gridObject.Hide();
				    }
				    break;
			    case Enums.FogState.PreviouslySeen:
				    foreach (var gridObject in gridObjects)
				    {
					    if (gridObject.scenery || gridObject.Team == Enums.UnitTeam.Player)
					    {
						    continue;
					    }

					    gridObject.Show();
				    }
				    break;
			    case Enums.FogState.Visible:
				    foreach (var gridObject in gridObjects)
				    {
					    if (gridObject.scenery || gridObject.Team == Enums.UnitTeam.Player)
					    {
						    continue;
					    }

					    gridObject.Show();
				    }
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
        this.WorldCenter = worldCenter;
    }

    public void SetState(Enums.GridCellState state)
    {
        if (this.state == state)
            return;

        this.state = state;

        GridSystem.Instance.UpdateGridCell(GridCoordinates);
        GridSystem.Instance.UpdateNeighborsConnections(this.GridCoordinates);
    }
    
    public void SetStateWithoutConnectionUpdate(Enums.GridCellState newState)
    {
	    this.state = newState;
    }

    public void AddGridObject(GridObject gridObject, Enums.GridCellState newState, bool rebuildConnections)
    {
        this.gridObjects ??= new List<GridObject>();
        this.gridObjects.Add(gridObject);

        bool stateChanged = this.state != newState;
        if (stateChanged)
            this.state = newState;
        UpdateGridObjectVisibility();
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
            GridSystem.Instance.UpdateGridCell(GridCoordinates);
            GridSystem.Instance.UpdateNeighborsConnections(this.GridCoordinates);
        }
        UpdateGridObjectVisibility();
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