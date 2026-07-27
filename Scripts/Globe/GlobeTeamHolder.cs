using System.Collections.Generic;
using System;
using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GlobeTeamHolder : Node
{
	public Enums.UnitTeam Team;
	public int funds;
	public int newbaseCost = 200000;
	public List<TeamBaseCellDefinition> Bases = new List<TeamBaseCellDefinition>();
	
	public Craft SelectedCraft { get; protected set; }
	
	public Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int> monthlyScore { get; private set; } = new();
	public int TotalMonthlyScore
	{
		get
		{
			int total = 0;
			foreach (int score in monthlyScore.Values)
				total += score;
			return total;
		}
	}
	[Signal] public delegate void FundsChangedEventHandler(GlobeTeamHolder teamHolder, int currentFunds);
	[Signal] public delegate void BaseAddedEventHandler(int hexCellIndex, GlobeTeamHolder teamHolder);
	[Signal] public delegate void BaseRemovedEventHandler(int hexCellIndex, GlobeTeamHolder teamHolder);
	[Signal] public delegate void MonthlyScoreChangedEventHandler(Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int> score);
	
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


	public void AddMonthlyScore(int amount, Enums.MonthlyScoreReason reason =  Enums.MonthlyScoreReason.None)
	{
		monthlyScore ??= new Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>();
		if (monthlyScore.ContainsKey(reason))
		{
			monthlyScore[reason] += amount;
		}
		else
		{
			monthlyScore.Add(reason, amount);
		}
		EmitSignal(SignalName.MonthlyScoreChanged, monthlyScore);
	}

	public void RemoveMonthlyScore(int amount, Enums.MonthlyScoreReason reason = Enums.MonthlyScoreReason.None)
	{
		monthlyScore ??= new Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>();
		if (monthlyScore.ContainsKey(reason))
		{
			monthlyScore[reason] -= amount;
		}
		else
		{
			monthlyScore.Add(reason, -(amount));
		}
		EmitSignal(SignalName.MonthlyScoreChanged, monthlyScore);
	}

	public void SetMonthlyScore(int amount, Enums.MonthlyScoreReason reason = Enums.MonthlyScoreReason.None)
	{
		monthlyScore ??= new Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>();
		if (monthlyScore.ContainsKey(reason))
		{
			monthlyScore[reason] = amount;
		}
		else
		{
			monthlyScore.Add(reason, amount);
		}
		EmitSignal(SignalName.MonthlyScoreChanged, monthlyScore);
	}

	public Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int> GetMonthlyScoreSnapshot()
	{
		var snapshot = new Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>();
		foreach (var score in monthlyScore)
			snapshot[score.Key] = score.Value;
		return snapshot;
	}

	public void ResetMonthlyScore()
	{
		monthlyScore.Clear();
		EmitSignal(SignalName.MonthlyScoreChanged, monthlyScore);
	}
	
	
	public bool TryBuildBase( HexCellData cell,  int cost)
	{
		if (!CanAffordCost(cost)) return false;

		TryRemoveFunds(cost);
		TeamBaseCellDefinition baseCellDefinition =
			new TeamBaseCellDefinition(cell.Index, "Base " + Bases.Count + 1, Team, null);
		Bases.Add(baseCellDefinition);
		EmitSignal(SignalName.BaseAdded, cell.Index, this);
		return true;
	}

	public Godot.Collections.Dictionary<string, Variant> Save()
	{
		var basesData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var b in Bases) basesData[b.cellIndex.ToString()] = b.Save();

		// Named keys keep the save format stable if enum ordering changes and
		// make the breakdown easy to inspect or migrate later.
		var monthlyScoreData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var score in monthlyScore)
			monthlyScoreData[score.Key.ToString()] = score.Value;

		return new Godot.Collections.Dictionary<string, Variant> {
			["team"] = (int)Team,
			["funds"] = funds,
			["monthlyScore"] = monthlyScoreData,
			["bases"] = basesData
		};
	}
	
	public async Task LoadAsync(Godot.Collections.Dictionary<string, Variant> data, Node unitParent)
	{
		if (data.ContainsKey("team"))
			Team = (Enums.UnitTeam)data["team"].AsInt32();

		if (data.ContainsKey("funds"))
			funds = data["funds"].AsInt32();

		LoadMonthlyScore(data);

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

	private void LoadMonthlyScore(Godot.Collections.Dictionary<string, Variant> data)
	{
		monthlyScore = new Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>();
		if (!data.ContainsKey("monthlyScore")) return;

		Variant savedScore = data["monthlyScore"];
		if (savedScore.VariantType == Variant.Type.Dictionary)
		{
			var scoreData = savedScore.AsGodotDictionary();
			foreach (var entry in scoreData)
			{
				string savedReason = entry.Key.VariantType == Variant.Type.Int
					? entry.Key.AsInt32().ToString()
					: entry.Key.AsString();
				if (!TryParseMonthlyScoreReason(savedReason, out Enums.MonthlyScoreReason reason))
				{
					GD.PrintErr($"Unknown monthly score reason in save: {savedReason}");
					continue;
				}

				monthlyScore[reason] = entry.Value.AsInt32();
			}
			return;
		}

		// Saves made before the reason breakdown stored one integer.
		if (savedScore.VariantType == Variant.Type.Int ||
		    savedScore.VariantType == Variant.Type.Float)
		{
			int legacyScore = savedScore.AsInt32();
			if (legacyScore != 0)
				monthlyScore[Enums.MonthlyScoreReason.None] = legacyScore;
		}
	}

	private static bool TryParseMonthlyScoreReason(
		string value,
		out Enums.MonthlyScoreReason reason)
	{
		if (int.TryParse(value, out int numericReason) &&
		    Enum.IsDefined(typeof(Enums.MonthlyScoreReason), numericReason))
		{
			reason = (Enums.MonthlyScoreReason)numericReason;
			return true;
		}

		return Enum.TryParse(value, true, out reason);
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
