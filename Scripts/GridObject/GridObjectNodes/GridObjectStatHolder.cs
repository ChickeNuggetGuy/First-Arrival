using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridObjectStatHolder : GridObjectNode
{
	private Godot.Collections.Dictionary<Enums.Stat, GridObjectStat> _stats = new Godot.Collections.Dictionary<Enums.Stat, GridObjectStat>();
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
				if (!_stats.ContainsKey(stat.Stat))
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
	
	public bool CanAffordStatCost(Godot.Collections.Dictionary<Enums.Stat, int> costs)
	{
		foreach (var stat in costs)
		{
			GridObjectStat statObj = Stats.FirstOrDefault(statObj => statObj.Stat == stat.Key);
			
			if (statObj == null) return false;
			
			if (stat.Value  > statObj.CurrentValue ) return false;
		}
		return true;
	}

	public bool TryRemoveStatCosts(Godot.Collections.Dictionary<Enums.Stat, int> costs)
	{
		foreach (var stat in costs)
		{
			GridObjectStat statObj = Stats.FirstOrDefault(s => s.Stat == stat.Key);
			if (statObj == null) continue;
			
			statObj.RemoveValue(stat.Value);
		}
		return true;
	}
	
	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var retVal =  new Godot.Collections.Dictionary<string, Variant>();

		foreach (var stat in Stats)
		{
			retVal.Add(stat.Stat.ToString(), stat.Save());
		}
		
		return retVal;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		// Re-setup stats collection first
		_stats.Clear();
		foreach (var child in this.GetChildren())
		{
			if (child is GridObjectStat stat)
			{
				_stats.Add(stat.Stat, stat);
			}
		}
    
		// Then load individual stat data
		foreach (var statDataEntry in data)
		{
			string statName = statDataEntry.Key;
			if (Enum.TryParse<Enums.Stat>(statName, out Enums.Stat statEnum))
			{
				if (_stats.ContainsKey(statEnum))
				{
					var statData = (Godot.Collections.Dictionary<string, Variant>)statDataEntry.Value;
					_stats[statEnum].Load(statData);
				}
			}
		}
	}
}
