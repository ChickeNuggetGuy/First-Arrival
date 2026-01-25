using Godot;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

public partial class GlobeTeamHolder : Node
{
	public Enums.UnitTeam Team;
	public int funds;
	public List<TeamBaseCellDefinition> Bases;
	
	public Craft SelectedCraft { get; protected set; }
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