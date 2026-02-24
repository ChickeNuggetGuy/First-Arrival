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
	[Export] Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder> gridObjectTeams = new Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder>();
	
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

	public override string GetManagerName() => "GridObjectManager";

	protected override Task _Setup(bool loadingData)
	{
		gridObjectTeams.Clear();
		foreach (Node child in GetChildren())
		{
			if (child is GridObjectTeamHolder teamHolder)
			{
				if (!HasLoadedData) teamHolder.Setup();
				
				if (!gridObjectTeams.ContainsKey(teamHolder.Team))
					gridObjectTeams.Add(teamHolder.Team, teamHolder);
			}
		}
		
		if (!HasLoadedData)
		{
			GD.Print("GridObjectManager: No data found in save. Initializing fresh spawn counts.");
			spawnCounts[Enums.UnitTeam.Player] = GameManager.Instance.unitCounts.X;
			spawnCounts[Enums.UnitTeam.Enemy] = GameManager.Instance.unitCounts.Y;
		}

		return Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{

		if (HasLoadedData)
		{
			//loaded saved units for this scene
			GridObjectTeamHolder teamHolder = GetGridObjectTeamHolder(Enums.UnitTeam.Player);
			if (teamHolder != null)
			{
				teamHolder.UpdateVisibility();
				if(teamHolder.CurrentGridObject == null) teamHolder.GetNextGridObject();
				teamHolder.SelectedGridObjectChanged += InventoryManager.Instance.TeamHolderOnSelectedGridObjectChanged;
			}
			return;
		}

		
		try
		{
			// Either New Game or  loaded a Globe save and entered a new Battle.
			foreach (KeyValuePair<Enums.UnitTeam, int> kvp in spawnCounts)
			{
				for (int i = 0; i < kvp.Value; i++)
					await TrySpawnGridObject(GetGridObjectTeamHolder(kvp.Key).unitPrefab, kvp.Key);

				if (gridObjectTeams[kvp.Key].GridObjects[Enums.GridObjectState.Active].Count > 0)
				{
					GetGridObjectTeamHolder(kvp.Key).SetSelectedGridObject(
						gridObjectTeams[kvp.Key].GridObjects[Enums.GridObjectState.Active][0]);
				}
			}

			GridObjectTeamHolder playerHolder = GetGridObjectTeamHolder(Enums.UnitTeam.Player);
			if (playerHolder != null)
			{
				playerHolder.UpdateVisibility();
				playerHolder.GetNextGridObject();
				playerHolder.SelectedGridObjectChanged += InventoryManager.Instance.TeamHolderOnSelectedGridObjectChanged;
			}
		}
		catch (Exception e) { GD.PrintErr(e); throw; }
	}

	private async Task TrySpawnGridObject(PackedScene gridObjectScene, Enums.UnitTeam team)
	{
		bool success;
		GridCell cell;

		if (team.HasFlag(Enums.UnitTeam.Player))
		{
			success = GridSystem.Instance.TryGetRandomGridCell(true, out cell, teamFilter: Enums.UnitTeam.Player);
		}
		else
		{
			success = GridSystem.Instance.TryGetRandomGridCell(true, out cell, Enums.GridCellState.None, true);
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
		gridObjectInstance.GlobalPosition = cell.WorldCenter;

		gridObjectInstance.Name = $"{Enum.GetName(team)}_{GetGridObjectTeamHolder(team).GridObjects[Enums.GridObjectState.Active].Count}_{Guid.NewGuid().ToString().Substring(0,4)}";
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

	#region Manager Data
	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		base.Load(data);

		gridObjectTeams.Clear();
		foreach (Node child in GetChildren())
		{
			if (child is GridObjectTeamHolder teamHolder && !gridObjectTeams.ContainsKey(teamHolder.Team))
				gridObjectTeams.Add(teamHolder.Team, teamHolder);
		}

		if (data == null) return;

		foreach (var dataKVP in data)
		{
			if (Enum.TryParse(dataKVP.Key, out Enums.UnitTeam team) && gridObjectTeams.ContainsKey(team))
			{
				var holderData = (Godot.Collections.Dictionary<string, Variant>)dataKVP.Value;
				gridObjectTeams[team].Load(holderData);
			}
		}
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		Godot.Collections.Dictionary<string, Variant> retVal = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var teamKVP in gridObjectTeams)
		{
			GridObjectTeamHolder teamHolder = teamKVP.Value;
			// Recursively call Save on the TeamHolder
			retVal.Add(Enum.GetName(teamKVP.Key), teamHolder.Save());
		}
		return retVal;
	}
	#endregion
	
	public override void Deinitialize()
	{
		return;
	}
}