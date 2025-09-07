using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;

public partial class GridCell 
{
	public static GridCell Null = new GridCell(
		new Vector3I(-1, -1, -1),
		new Vector3(-1, -1, -1),
		Enums.GridCellState.None,
		Enums.FogState.Visible, null
	);
	
	public Vector3I gridCoordinates { get; protected set; }
	public Vector3 worldCenter { get; protected set; }
	
	public List<CellConnection> connections { get; protected set; }
	
	public Enums.GridCellState originalState { get; protected set; }
	public Enums.GridCellState state { get; protected set; }
	public Enums.FogState fogState { get; protected set; }
	
	public Enums.UnitTeam UnitTeamSpawn {get; protected set;} = Enums.UnitTeam.None;
	public GridObject currentGridObject { get; protected set; }
	
	public InventoryGrid InventoryGrid { get; protected set; }
	public GridCell(Vector3I gridCoordinates, Vector3 worldCenter, Enums.GridCellState state, Enums.FogState fogState,
		InventoryGrid inventory,Enums.UnitTeam unitTeamSpawn = Enums.UnitTeam.All)
	{
		this.gridCoordinates = gridCoordinates;
		this.worldCenter = worldCenter;
		this.state = state;
		this.originalState = state;
		this.fogState = fogState;
		connections = new List<CellConnection>();
		unitTeamSpawn = unitTeamSpawn;
		InventoryGrid = inventory;
	}

	public bool HasGridObject()
	{
		return (currentGridObject != null);
	}
	
	public void SetState(Enums.GridCellState state)
	{
		this.state = state;
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
	public void SetGridObject(GridObject gridObject, Enums.GridCellState newState)
	{
		this.currentGridObject = gridObject;
		this.state = newState;
	}

	public void SetConnections(List<CellConnection> connections)
	{
		this.connections = connections;
	}
	public void RestoreOriginalState()
	{
		state = originalState;
		currentGridObject = null;
	}
}
