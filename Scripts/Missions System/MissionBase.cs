using Godot;
using System;
using FirstArrival.Scripts.Utility;

public abstract partial class MissionBase(Enums.MissionType missionType, int enemySpawnCount, int cellIndex) : Resource
{
	public Enums.MissionType MissionType = missionType;
	public int cellIndex = cellIndex;

	public int EnemySpawnCount = enemySpawnCount;
	
	
	
	public virtual Godot.Collections.Dictionary<string, Variant> Save()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "type", (int)missionType },
			{ "enemyCount", EnemySpawnCount }
		};
	}
}
