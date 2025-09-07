using Godot;
using System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridCellStateOverride : Area3D
{
	[Export] bool useGridCellStateOverride = false;
	[Export]Enums.GridCellState cellStateOverride;
	
	[Export] bool usefogStateOverride = false;
	[Export]Enums.FogState fogState;
	
	[Export] bool useunitTeamSpawnOverride = false;
	[Export]Enums.UnitTeam unitTeamSpawn;

	public override void _Ready()
	{
		base._Ready();
		GridSystem.Instance.GridSystemInitialized += InstanceOnGridSystemInitialized;
	}

	private void InstanceOnGridSystemInitialized()
	{
		if(!GridSystem.Instance.TryGetGridCellsInArea(this, out var gridCells))
		{
			return;
		}
		if(!useGridCellStateOverride  && !usefogStateOverride && !useunitTeamSpawnOverride)return;
		
		foreach (GridCell gridCell in gridCells)
		{
			if(useGridCellStateOverride)
				gridCell.SetState(cellStateOverride);
			
			if(usefogStateOverride)
				gridCell.SetFogState(fogState);
			
			if(useunitTeamSpawnOverride)
				gridCell.SetUnitSpawnState(unitTeamSpawn);
			
		}
		
	}
}
