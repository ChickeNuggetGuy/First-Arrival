using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GlobeTeamHolder : Node
{
	public Enums.UnitTeam Team;
	public int funds;
	public List<TeamBaseCellDefinition> Bases = new List<TeamBaseCellDefinition>();
	
	public Craft SelectedCraft { get; protected set; }
	[Signal] public delegate void FundsChangedEventHandler(GlobeTeamHolder teamHolder, int currentFunds);
	[Signal] public delegate void BaseAddedEventHandler(int hexCellIndex, GlobeTeamHolder teamHolder);
	[Signal] public delegate void BaseRemovedEventHandler(int hexCellIndex, GlobeTeamHolder teamHolder);
	
	public GlobeTeamHolder(Enums.UnitTeam affiliation, List<TeamBaseCellDefinition> bases, int startingFunds = 1000000)
	{
		Team = affiliation;
		Bases = bases ?? new List<TeamBaseCellDefinition>();
		funds = startingFunds;
	}

	public GlobeTeamHolder() : this(Enums.UnitTeam.None, new List<TeamBaseCellDefinition>(), 0) { }

	public bool CanAffordCost(int cost) => funds >= cost;

	public bool TryRemoveFunds(int amount)
	{
		if (funds < amount) return false;
		funds -= amount;
		GD.Print("Try remove funds: " + funds);
		EmitSignal(SignalName.FundsChanged, this, funds);
		return true;
	}
	
	public bool TryBuildBase( HexCellData cell, int baseIndex,  int cost)
	{
		if (!CanAffordCost(cost)) return false;

		TryRemoveFunds(cost);
		TeamBaseCellDefinition baseCellDefinition =
			new TeamBaseCellDefinition(cell.Index, "Base " + baseIndex.ToString(), Team, null);
		Bases.Add(baseCellDefinition);
		EmitSignal(SignalName.BaseAdded, cell.Index, this);
		return true;
	}

	public Godot.Collections.Dictionary<string, Variant> Save()
	{
		var basesData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var b in Bases) basesData[b.cellIndex.ToString()] = b.Save();

		return new Godot.Collections.Dictionary<string, Variant> {
			["team"] = (int)Team,
			["funds"] = funds,
			["bases"] = basesData
		};
	}
	
	public async Task LoadAsync(Godot.Collections.Dictionary<string, Variant> data, Node unitParent)
	{
		if (data.ContainsKey("team"))
			Team = (Enums.UnitTeam)data["team"].AsInt32();

		if (data.ContainsKey("funds"))
			funds = data["funds"].AsInt32();

		if (data.ContainsKey("bases"))
		{
			var basesData = data["bases"].AsGodotDictionary<string, Variant>();
			Bases.Clear();

			foreach (var kvp in basesData)
			{
				int cellIndex = int.Parse(kvp.Key);
				var baseData = kvp.Value.AsGodotDictionary<string, Variant>();

				string baseName = baseData.ContainsKey("definitionName")
					? baseData["definitionName"].AsString()
					: "Loaded Base";

				TeamBaseCellDefinition newBase = new TeamBaseCellDefinition(
					cellIndex, baseName, Team, null
				);

				await newBase.LoadAsync(baseData, unitParent); // <-- the actual fix
				GD.Print("Loaded Base: " + baseName);
				Bases.Add(newBase);
			}
		}
	}
	
	public bool TryGetBaseAtIndex(int cellIndex, out TeamBaseCellDefinition teamBase)
	{
		teamBase = null;
		if (Bases == null) return false;

		for (int i = 0; i < Bases.Count; i++)
		{
			TeamBaseCellDefinition baseDef = Bases[i];
			
			if(baseDef == null) continue;

			if (baseDef.cellIndex == cellIndex)
			{
				teamBase = baseDef;
				return true;
			}
		}

		return false;
	}

	#region Get/Set Functions

	public Craft GetCraft() => SelectedCraft;
	public void SetSelectedCraft(Craft craft) => SelectedCraft = craft;

	#endregion
}