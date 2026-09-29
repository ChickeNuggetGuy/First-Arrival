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
	[Export] Godot.Collections.Dictionary<Enums.UnitTeam, int> spawnCounts = new();
	[Export] Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder> gridObjectTeams = new();
	[Export] private StartingEuipmentUI startingEuipmentUI;
	private readonly System.Collections.Generic.Dictionary<
		Enums.UnitTeam,
		Godot.Collections.Dictionary<string, Variant>> _loadedTeamData = new();


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
			foreach (var pair in gridObjectTeams)
			{
				if (_loadedTeamData.TryGetValue(pair.Key, out var holderData))
					await pair.Value.LoadAsync(holderData);
				else
					pair.Value.Setup();
			}
			_loadedTeamData.Clear();
			RestoreBodyLinks();

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
			// Either New Game or loaded a Globe save and entered a new Battle.
			foreach (KeyValuePair<Enums.UnitTeam, int> kvp in spawnCounts)
			{
				bool useCraftUnits = kvp.Key == Enums.UnitTeam.Player &&
				                     GameManager.Instance.HasPendingBattlePlayerUnits;

				if (useCraftUnits)
				{
					var savedUnits = GameManager.Instance.GetPendingBattlePlayerUnits();
					foreach (var savedUnit in savedUnits)
						await TrySpawnSavedGridObject(savedUnit, kvp.Key);
				}
				else
				{
					for (int i = 0; i < kvp.Value; i++)
						await TrySpawnGridObject(GetGridObjectTeamHolder(kvp.Key).unitPrefabs.PickRandom(), kvp.Key);
				}

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
		
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		await startingEuipmentUI.ShowCall();
		await ToSignal(startingEuipmentUI, StartingEuipmentUI.SignalName.AcceptPressed);
		GameManager.Instance.ClearPendingBattleLoadout();
	}

	private async Task TrySpawnSavedGridObject(
		Godot.Collections.Dictionary<string, Variant> unitData,
		Enums.UnitTeam team)
	{
		var battleUnitData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var pair in unitData)
			battleUnitData[pair.Key] = pair.Value;

		// Units stored on a craft are inactive and have no globe-grid position.
		// Override only that scene-specific state; their stats and inventories remain.
		battleUnitData["Team"] = (int)team;
		battleUnitData["IsActive"] = true;
		battleUnitData["HasPosition"] = false;

		GridObjectTeamHolder teamHolder = GetGridObjectTeamHolder(team);
		GridObject instance = await GridObjectSerializationUtility.LoadGridObjectAsync(
			battleUnitData,
			teamHolder,
			false
		);
		if (instance == null) return;
		if (!TryGetFootprintSpawnCell(instance, team, out GridCell cell))
		{
			GD.PrintErr($"No spawn area can fit the saved {team} unit's footprint.");
			instance.QueueFree();
			return;
		}

		instance.GlobalPosition = cell.WorldCenter;
		instance.SetIsActive(true);
		instance.Show();
		instance.GridPositionData.SetGridCell(cell);
		await teamHolder.AddGridObject(instance);
	}

	private bool TryGetSpawnCell(Enums.UnitTeam team, out GridCell cell)
	{
		if (team.HasFlag(Enums.UnitTeam.Player))
		{
			return GridSystem.Instance.TryGetRandomGridCell(
				true,
				out cell,
				teamFilter: Enums.UnitTeam.Player
			);
		}

		return GridSystem.Instance.TryGetRandomGridCell(
			true,
			out cell,
			Enums.GridCellState.None,
			true
		);
	}

	private bool TryGetFootprintSpawnCell(
		GridObject gridObject,
		Enums.UnitTeam team,
		out GridCell cell
	)
	{
		cell = null;
		GridPositionData positionData = gridObject?.GridPositionData
		                                ?? gridObject?.GetNodeOrNull<GridPositionData>(
			                                "GridPositionData"
		                                );
		if (positionData == null)
			return TryGetSpawnCell(team, out cell);

		IEnumerable<GridCell> candidates = GridSystem.Instance.AllGridCells.Where(
			candidate => candidate != null &&
			             candidate.IsWalkable &&
			             !candidate.HasSpawnBlockingGridObject()
		);
		if (team.HasFlag(Enums.UnitTeam.Player))
		{
			candidates = candidates.Where(
				candidate => candidate.UnitTeamSpawn == Enums.UnitTeam.Player
			);
		}

		foreach (GridCell candidate in candidates.OrderBy(_ => GD.Randf()))
		{
			if (!positionData.CanOccupyAt(candidate, positionData.Direction))
				continue;

			cell = candidate;
			return true;
		}

		return false;
	}

	private async Task TrySpawnGridObject(PackedScene gridObjectScene, Enums.UnitTeam team)
	{
		GridObject gridObjectInstance = gridObjectScene.Instantiate() as GridObject;
		if (gridObjectInstance == null)
		{
			GD.Print("Grid Object Not Found");
			return;
		}

		if (!TryGetFootprintSpawnCell(gridObjectInstance, team, out GridCell cell))
		{
			GD.Print("No spawn area can fit the grid object's footprint");
			gridObjectInstance.Free();
			return;
		}

		gridObjectTeams[team].AddChild(gridObjectInstance);
		gridObjectInstance.GlobalPosition = cell.WorldCenter;

		gridObjectInstance.Name = UnitNameGenerator.Generate();
		GD.PrintErr("Initalizing Grid Object");
		await gridObjectInstance.Initialize(team, cell);
		await gridObjectTeams[team].AddGridObject(gridObjectInstance);
	}


	public GridObjectTeamHolder GetGridObjectTeamHolder(Enums.UnitTeam team)
	{
		if (!gridObjectTeams.ContainsKey(team)) return null;
		return gridObjectTeams[team];
	}

	public ActionBase SetCurrentGridObject(Enums.UnitTeam team, GridObject gridObject)
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
	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		_loadedTeamData.Clear();
		gridObjectTeams.Clear();
		foreach (Node child in GetChildren())
		{
			if (child is GridObjectTeamHolder teamHolder && !gridObjectTeams.ContainsKey(teamHolder.Team))
				gridObjectTeams.Add(teamHolder.Team, teamHolder);
		}

		if (data == null) return Task.CompletedTask;

		foreach (var dataKVP in data)
		{
			if (Enum.TryParse(dataKVP.Key, out Enums.UnitTeam team) && gridObjectTeams.ContainsKey(team))
			{
				var holderData = (Godot.Collections.Dictionary<string, Variant>)dataKVP.Value;
				_loadedTeamData[team] = holderData;
			}
		}
		return Task.CompletedTask;
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
