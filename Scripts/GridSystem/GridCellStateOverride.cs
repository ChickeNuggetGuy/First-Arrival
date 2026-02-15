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
	

	public override void _EnterTree()
	{
		base._EnterTree();
		AddToGroup("GridObjects");
		AddToGroup("GridCellOverride");
	}

	public void InitializeGridCellOverride()
	{
		if (this.GridPositionData == null)
		{
			if (GridObjectNodeHolder == null)
			{
				GridPositionData = GetNodeOrNull<GridPositionData>("GridPositionData");
			}
			else
			{
				GridPositionData = GridObjectNodeHolder.GetNodeOrNull<GridPositionData>("GridPositionData");
			}
		}

		if (this.GridPositionData == null)
		{
			GD.PrintErr($"GridCellStateOverride {Name} failed: GridPositionData is null.");
			return;
		}

		// DEBUG: Check shape status
		GridShape shape = this.GridPositionData.Shape;
		GD.Print($"[{Name}] GridPositionData found: {GridPositionData != null}");
		GD.Print($"[{Name}] Shape: {(shape != null ? $"Size({shape.SizeX}x{shape.SizeY}x{shape.SizeZ})" : "NULL")}");
    
		if (shape != null)
		{
			int occupiedCount = 0;
			for (int y = 0; y < shape.SizeY; y++)
				for (int x = 0; x < shape.SizeX; x++)
					for (int z = 0; z < shape.SizeZ; z++)
						if (shape.IsOccupied(x, y, z))
							occupiedCount++;
			GD.Print($"[{Name}] Occupied cells in shape: {occupiedCount}");
		}
		
		GridCell rootCell = this.GridPositionData.AnchorCell;
		if (rootCell == null)
		{
			if (GridSystem.Instance.TryGetGridCellFromWorldPosition(this.GridPositionData.GlobalPosition, out GridCell nearest, true))
			{
				rootCell = nearest;
				GD.Print($"GridCellStateOverride {Name}: Snapped to nearest cell {rootCell.GridCoordinates}");
			}
		}

		if (rootCell == null)
		{
			GD.PrintErr($"GridCellStateOverride {Name}: Could not determine root GridCell.");
			return;
		}
		
		if (!rootCell.state.HasFlag(Enums.GridCellState.Ground))
		{
			for (int i = 1; i <= 10; i++)
			{
				GridCell below = GridSystem.Instance.GetGridCell(rootCell.GridCoordinates + new Vector3I(0, -i, 0));
				if (below != null && below.state.HasFlag(Enums.GridCellState.Ground))
				{ 
					rootCell = below;
					break;
				}
			}
		}

		if (!useGridCellStateOverride && !usefogStateOverride && !useUnitTeamSpawnOverride) return;
		
		List<GridCell> gridCells = new List<GridCell>();
		Vector3I rootCoords = rootCell.GridCoordinates;
		Enums.Direction direction = this.GridPositionData.Direction;

		if (shape != null)
		{
			for (int y = 0; y < shape.SizeY; y++)
			{
				for (int x = 0; x < shape.SizeX; x++)
				{
					for (int z = 0; z < shape.SizeZ; z++)
					{
						if (!shape.IsOccupied(x, y, z))
							continue;

						int relX = x - shape.PivotCell.X;
						int relZ = z - shape.PivotCell.Y;
						int offsetY = y;

						int rotatedX = relX;
						int rotatedZ = relZ;

						switch (direction)
						{
							case Enums.Direction.North:
								rotatedX = relX;
								rotatedZ = relZ;
								break;
							case Enums.Direction.East:
								rotatedX = -relZ;
								rotatedZ = relX;
								break;
							case Enums.Direction.South:
								rotatedX = -relX;
								rotatedZ = -relZ;
								break;
							case Enums.Direction.West:
								rotatedX = relZ;
								rotatedZ = -relX;
								break;
						}

						Vector3I targetCoords = rootCoords + new Vector3I(rotatedX, offsetY, rotatedZ);
						GridCell cell = GridSystem.Instance.GetGridCell(targetCoords);

						if (cell != null)
						{
							gridCells.Add(cell);
						}
					}
				}
			}
		}
		else
		{
			// Fallback if no shape, just use root
			gridCells.Add(rootCell);
		}


		
		List<GridCell> changedCells  = new List<GridCell>();
		foreach (GridCell gridCell in gridCells)
		{
			if (useGridCellStateOverride)
			{
				bool passFilter = cellStateOverrideFilter == Enums.GridCellState.None || 
				                  gridCell.state.HasFlag(cellStateOverrideFilter);

				if (passFilter)
				{
					if(!changedCells.Contains(gridCell))
						changedCells.Add(gridCell);
					gridCell.SetStateWithoutConnectionUpdate(gridCell.state | cellStateOverride);
				}
			}

			// Fog State Override
			if (usefogStateOverride)
			{
				bool passFilter = fogStateOverrideFilter == Enums.GridCellState.None || 
				                  gridCell.state.HasFlag(fogStateOverrideFilter);
            
				if (passFilter)
				{
					if(!changedCells.Contains(gridCell))
						changedCells.Add(gridCell);
					gridCell.SetFogState(fogState);
				}
			}

			//Unit Team Spawn Override
			if (useUnitTeamSpawnOverride)
			{
				bool passFilter = teamSpawnStateOverrideFilter == Enums.GridCellState.None || 
				                  gridCell.state.HasFlag(teamSpawnStateOverrideFilter);

				if (passFilter)
				{
					if(!changedCells.Contains(gridCell))
						changedCells.Add(gridCell);
					gridCell.SetUnitSpawnState(unitTeamSpawn);
				}
			}
		}
		GD.Print($"GridCellStateOverride {Name} applied to {changedCells.Count} cells.");
	}
	
	
	
}
