using Godot;
using System;
using FirstArrival.Scripts.Utility;

public partial class TeamBaseCellDefinition : HexCellDefinition
{
	public Enums.UnitTeam teamAffiliation = Enums.UnitTeam.None;

	public TeamBaseCellDefinition(int cellIndex, Enums.UnitTeam team) : base(cellIndex)
	{
		this.teamAffiliation = team;
	}
	
	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		// Get the base data (cellIndex)
		var data = base.Save();
        
		// Add specific data for this class
		data.Add("teamAffiliation", (int)teamAffiliation);
        
		return data;
	}
}
