using Godot;
using System;
using FirstArrival.Scripts.Utility;

public partial class EliminateMission : MissionBase
{
	public EliminateMission(Enums.MissionType MissionType, int EnemySpawnRange, int cellIndex) : base(MissionType, EnemySpawnRange, cellIndex)
	{
	}
}
