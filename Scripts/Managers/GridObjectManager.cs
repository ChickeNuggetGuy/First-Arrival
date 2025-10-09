using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class GridObjectManager : Manager<GridObjectManager>
{
	[Export] Godot.Collections.Dictionary<Enums.UnitTeam, int> spawnCounts = new Godot.Collections.Dictionary<Enums.UnitTeam, int>();
	[Export]Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder> gridObjectTeams = new Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder>();
	
	[Export] PackedScene gridObjectScene;
	public GridObject CurrentPlayerGridObject
	{
		get
		{
			GridObjectTeamHolder holder = GetGridObjectTeamHolder(Enums.UnitTeam.Player);
			if (holder == null) return null;
			
			return holder.CurrentGridObject;
		}
	}
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey k || !k.Pressed) return;

		var holder = GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (holder == null) return;

		if (k.Keycode == Key.V)
		{
			holder.UpdateGridObjects(null, null);
		}
		else if (k.Keycode == Key.C)
		{
			holder.GetNextGridObject();
		}
	}

	protected override Task _Setup()
	{
		foreach (Node child in GetChildren())
		{
			if (child is GridObjectTeamHolder teamHolder)
			{
				teamHolder.Setup();
				if (!gridObjectTeams.ContainsKey(teamHolder.Team))
				{
					gridObjectTeams.Add(teamHolder.Team, teamHolder);
				}
			}
		}

		spawnCounts[Enums.UnitTeam.Player] = GameManager.Instance.unitCounts.X;
		spawnCounts[Enums.UnitTeam.Enemy] = GameManager.Instance.unitCounts.Y;
		return Task.CompletedTask;
	}

	protected override  async Task _Execute()
	{
		foreach (KeyValuePair<Enums.UnitTeam, int> kvp in spawnCounts)
		{
			for(int i = 0; i <kvp.Value; i++)
				await TrySpawnGridObject(gridObjectScene, kvp.Key);
			
			if(gridObjectTeams[kvp.Key].GridObjects[Enums.GridObjectState.Active].Count > 0)
			{
				GetGridObjectTeamHolder(kvp.Key)
					.SetSelectedGridObject(gridObjectTeams[kvp.Key].GridObjects[Enums.GridObjectState.Active][0]);
			}
		}

		
		    GridObjectTeamHolder teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (teamHolder == null) return;
		teamHolder.UpdateVisibility();
		teamHolder.GetNextGridObject();
		teamHolder.SelectedGridObjectChanged += InventoryManager.Instance.TeamHolderOnSelectedGridObjectChanged;


	}

		private async Task TrySpawnGridObject(PackedScene gridObjectScene, Enums.UnitTeam team)
	{
		bool success;
		GridCell cell;

		if (team.HasFlag(Enums.UnitTeam.Player))
		{
			success = GridSystem.Instance.TryGetRandomGridCell(true,out cell, teamFilter: Enums.UnitTeam.Player);
		}
		else
		{
			success = GridSystem.Instance.TryGetRandomGridCell(true, out cell,Enums.GridCellState.None, true);
		}

		if (!success)
		{
			GD.Print("Grid Cell Not Found");
			return;
		}

		GridObject gridObjectInstance = gridObjectScene.Instantiate() as GridObject;
		if (gridObjectInstance == null)
		{
			GD.Print("Grid Object Not Found");
			return;
		}

		gridObjectTeams[team].AddGridObject(gridObjectInstance);
		gridObjectTeams[team].AddChild(gridObjectInstance);
		gridObjectInstance.GlobalPosition = cell.worldCenter;
		gridObjectInstance.Name = $"{Enum.GetName(team)}  {GetGridObjectTeamHolder(team).GridObjects[Enums.GridObjectState.Active].Count}";
		await gridObjectInstance.Initialize(team, cell);
	}
	

	public GridObjectTeamHolder GetGridObjectTeamHolder(Enums.UnitTeam team)
	{
		if (!gridObjectTeams.ContainsKey(team)) return null;
		return gridObjectTeams[team];
	}

	public Action SetCurrentGridObject(Enums.UnitTeam team, GridObject gridObject)
	{
		if (!gridObjectTeams.ContainsKey(team))
			return null;
		if (gridObject == null) 
			return null;
		
		gridObjectTeams[team].SetSelectedGridObject(gridObject);
		return null;
	}
	
	public Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder> GetGridObjectTeamHolders() => gridObjectTeams;
	
	#region manager Data
	protected override void GetInstanceData(ManagerData data)
	{
		GD.Print("No data to transfer");
	}

	public override ManagerData SetInstanceData()
	{
		return null;
	}
	#endregion
	
	
}
