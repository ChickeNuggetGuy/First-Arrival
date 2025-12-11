using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridObjectStatHolder : GridObjectNode
{
	private Dictionary<Enums.Stat, GridObjectStat> _stats = new Dictionary<Enums.Stat, GridObjectStat>();
	public List<GridObjectStat> Stats
	{
		get
		{
			return _stats.Values.ToList();
		}
		private set{}
	}


	protected override void Setup()
	{
		foreach (var child in this.GetChildren())
		{
			if (child is GridObjectStat stat)
			{
				_stats.Add(stat.Stat, stat);
			}
		}
	}
	
	
	public bool TryGetStat(Enums.Stat statToFind, out GridObjectStat stat)
	{
		stat = null;

		stat = _stats.FirstOrDefault(statKVP =>
		{
			if(statKVP.Key == statToFind) return true;
			return false;
		}).Value;
		
		if (stat == null) return false;
		else return true;
	}
	
	public bool CanAffordStatCost(System.Collections.Generic.Dictionary<Enums.Stat, int> costs)
	{
		foreach (var stat in costs)
		{
			GridObjectStat statObj = Stats.FirstOrDefault(statObj => statObj.Stat == stat.Key);
			
			if (statObj == null) return false;
			
			if (stat.Value  > statObj.CurrentValue ) return false;
		}
		return true;
	}
}
