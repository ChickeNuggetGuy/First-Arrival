using Godot;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

public partial class GlobeTeamHolder : Node
{
	public Enums.UnitTeam Team;
	public int funds;
	public List<TeamBaseCellDefinition> Bases;

	[Signal] public delegate void FundsChangedEventHandler(GlobeTeamHolder teamHolder, int currentFunds);

	public GlobeTeamHolder(Enums.UnitTeam affiliation, List<TeamBaseCellDefinition> bases, int startingFunds = 1000000)
	{
		Team = affiliation;
		Bases = bases ?? new List<TeamBaseCellDefinition>();
		funds = startingFunds;
	}

	public GlobeTeamHolder() : this(Enums.UnitTeam.None, new List<TeamBaseCellDefinition>(), 0) { }

	public bool CanAffordCost(int cost) => funds >= cost;

	public void TryRemoveFunds(int amount)
	{
		funds -= amount;
		GD.Print("Try remove funds: " + funds);
		EmitSignal(SignalName.FundsChanged, this, funds);
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
}