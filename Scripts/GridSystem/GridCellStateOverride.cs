using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridCellStateOverride : GridObject
{
	[ExportGroup("Cell State Override"),Export]protected  bool useGridCellStateOverride = false;
	[Export] protected Enums.GridCellState cellStateOverride;
	[Export]protected Enums.GridCellState cellStateOverrideFilter;
	
	[ExportGroup("Fog State Override"),Export]protected  bool usefogStateOverride = false;
	[Export]protected Enums.FogState fogState;
	[Export]Enums.GridCellState fogStateOverrideFilter;
	
	[ExportGroup("Cell Spawn Override"),Export]protected  bool useUnitTeamSpawnOverride = false;
	[Export]protected Enums.UnitTeam unitTeamSpawn;
	[Export]protected Enums.GridCellState teamSpawnStateOverrideFilter;

	public override void _Ready()
	{
		base._Ready();
	}

	public void InitializeGridCellOverride()
	{
		if (this.GridPositionData == null) return;

		List<GridCell> gridCells = GridSystem.Instance.GetCellsFromGridShape(this.GridPositionData);
		if (gridCells.Count < 1)
		{
			return;
		}

		if (!useGridCellStateOverride && !usefogStateOverride && !useUnitTeamSpawnOverride) return;

		foreach (GridCell gridCell in gridCells)
		{
			// 1. Grid Cell State Override
			if (useGridCellStateOverride)
			{
				// Logic: If filter is None, apply to all. OR if filter matches.
				bool passFilter = cellStateOverrideFilter == Enums.GridCellState.None || 
				                  gridCell.state.HasFlag(cellStateOverrideFilter);

				if (passFilter)
					gridCell.SetState(cellStateOverride);
			}

			// 2. Fog State Override
			if (usefogStateOverride)
			{
				// FIX: Use fogStateOverrideFilter, not cellStateOverrideFilter
				bool passFilter = fogStateOverrideFilter == Enums.GridCellState.None || 
				                  gridCell.state.HasFlag(fogStateOverrideFilter);
            
				if (passFilter)
					gridCell.SetFogState(fogState);
			}

			// 3. Unit Team Spawn Override
			if (useUnitTeamSpawnOverride)
			{
				// FIX: Use teamSpawnStateOverrideFilter, not cellStateOverrideFilter
				bool passFilter = teamSpawnStateOverrideFilter == Enums.GridCellState.None || 
				                  gridCell.state.HasFlag(teamSpawnStateOverrideFilter);

				if (passFilter)
					GD.Print($"Team Spawn set to {unitTeamSpawn}");
					gridCell.SetUnitSpawnState(unitTeamSpawn);
			}
		}
	}
}
