using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GlobeTeamManager : Manager<GlobeTeamManager>
{
	[Export] private PackedScene baseScene;
	[Export] private Node baseContainer;
	public bool buildBaseMode = false;
	
	[Export] private Enums.UnitTeam teams = Enums.UnitTeam.None;
	
	
	private Dictionary<Enums.UnitTeam, GlobeTeamHolder> teamData = 
		new Dictionary<Enums.UnitTeam, GlobeTeamHolder>();
	
	
	public override string GetManagerName() => "GlobeTeamManager";
	
	
	protected override async Task _Setup(bool loadingData)
	{
		teamData ??= new Dictionary<Enums.UnitTeam, GlobeTeamHolder>();
		if (loadingData && teamData.Count != 0) return;

		foreach (var team in Enum.GetValues(typeof(Enums.UnitTeam)))
		{
			if (teams.HasFlag((Enums.UnitTeam)team))
			{
				if((Enums.UnitTeam)team == Enums.UnitTeam.None) continue;
				
				var holder = new GlobeTeamHolder((Enums.UnitTeam)team, new List<TeamBaseCellDefinition>());
				teamData[(Enums.UnitTeam)team] = holder;
				AddChild(holder);
			}
		}
	}

	protected override async Task _Execute(bool loadingData)
	{
		if (loadingData)
		{
			foreach (var holder in teamData.Values)
			{
				foreach (var bDef in holder.Bases)
				{
					var cell = GlobeHexGridManager.Instance.GetCellFromIndex(bDef.cellIndex);
					if (cell.HasValue) SpawnBase(cell.Value, $"Base_{holder.Team}_{bDef.cellIndex}");
				}
			}
		}
		await Task.CompletedTask;
	}

	
	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		
		base.Load(data);
		
		if(!HasLoadedData) return;
		
		if (baseContainer != null) foreach (Node c in baseContainer.GetChildren()) c.QueueFree();
		foreach (var h in teamData.Values) h.QueueFree();
		teamData.Clear();

		if (!data.ContainsKey("teamData")) return;
		var teamHolderData = data["teamData"].AsGodotDictionary<string, Variant>();

		foreach (var kvp in teamHolderData)
		{
			if (!Enum.TryParse(kvp.Key, out Enums.UnitTeam team)) continue;
			var tData = kvp.Value.AsGodotDictionary<string, Variant>();
			
			int loadedFunds = tData["funds"].AsInt32();
			var basesDict = tData["bases"].AsGodotDictionary<string, Variant>();
			List<TeamBaseCellDefinition> defs = new();

			foreach (var bKvp in basesDict)
			{
				var bData = bKvp.Value.AsGodotDictionary<string, Variant>();
				defs.Add(new TeamBaseCellDefinition(bData["cellIndex"].AsInt32(), team));
			}

			var holder = new GlobeTeamHolder(team, defs, loadedFunds);
			teamData[team] = holder;
			AddChild(holder);
		}
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var teamHolderData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var kvp in teamData)
		{
			GD.Print("Saving team holder: " + kvp.Key.ToString());
			teamHolderData[kvp.Key.ToString()] = kvp.Value.Save();
		}
		return new Godot.Collections.Dictionary<string, Variant> { ["teamData"] = teamHolderData };
	}


	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if(!buildBaseMode) return;
		
		
		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed &&
		    mouseButton.ButtonIndex == MouseButton.Left)
		{
			//Left click in build Mode, Attempt to place base at current grid Cell
			HexCellData? cell = GlobeInputManager.Instance.CurrentCell;
			
			if (cell == null) return;

			if (TryBuildBase(Enums.UnitTeam.Player, cell.Value, 400000))
			{
				GD.Print("Building base");
			}
			else
			{
				GD.Print("Building failed");
			}
		}
	}

	
	public bool TryBuildBase(Enums.UnitTeam team, HexCellData cell, int cost)
	{
		if (team == Enums.UnitTeam.None) return false;
		if (!teamData.ContainsKey(team)) teamData[team] = new GlobeTeamHolder(team, new());
		if (!teamData[team].CanAffordCost(cost)) return false;

		teamData[team].TryRemoveFunds(cost);
		teamData[team].Bases.Add(new TeamBaseCellDefinition(cell.Index, team));
		SpawnBase(cell, "newBase");
		buildBaseMode = false;
		return true;
	}
	
	
	public bool TryBuildBase(Enums.UnitTeam team, int cellIndex, int cost)
	{
		if (cellIndex == -1) return false;
		HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromIndex(cellIndex);
		if (cell == null) return false;
		if (team ==  Enums.UnitTeam.None) return false;

		if (!teamData.ContainsKey(team))
		{
			teamData.Add(team, new GlobeTeamHolder(team, new List<TeamBaseCellDefinition>()));
		}
		
		if(!teamData[team].CanAffordCost(cost)) return false;
		
		BuildBase(team, cell.Value, cost);
		return true;

	}
	
	private void BuildBase(Enums.UnitTeam team, HexCellData cell, int cost)
	{
		TeamBaseCellDefinition newBase = new TeamBaseCellDefinition(cell.Index, team);

		teamData[team].TryRemoveFunds(cost);
		
		teamData[team].Bases.Add(newBase);
		SpawnBase(cell, "newBase");
		buildBaseMode = false;
	}
	
	
	private void SpawnBase(HexCellData cell, string name)
	{
		if (baseScene == null) return;
		var instance = baseScene.Instantiate<Node3D>();
		if (baseContainer != null) baseContainer.AddChild(instance);
		else AddChild(instance);

		instance.GlobalPosition = cell.Center;
		Vector3 normal = cell.Center.Normalized();
		Vector3 up = Mathf.Abs(normal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
		instance.LookAt(cell.Center + normal, up);
		instance.Name = name;
	}



	public Dictionary<Enums.UnitTeam, GlobeTeamHolder> GetAllTeamData() => teamData;

	public GlobeTeamHolder GetTeamData(Enums.UnitTeam team) => teamData.GetValueOrDefault(team, null);
	
	public override void Deinitialize()
	{
		return;
	}
}



