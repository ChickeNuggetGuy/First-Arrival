using Godot;
using System;
using FirstArrival.Scripts.Utility;

public partial class EliminateMission : MissionBase
{
	public EliminateMission(
		string name,
		string description,
		Enums.MissionType missionType,
		int difficulty,
		int enemySpawnRange,
		int cellIndex,
		Enums.MissionRecoveryType recoveryType =
			Enums.MissionRecoveryType.FullFieldOnSuccess)
		: base(
			name,
			description,
			missionType,
			difficulty,
			enemySpawnRange,
			cellIndex,
			recoveryType)
	{
	}
}
