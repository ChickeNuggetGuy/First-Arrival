using System;
using FirstArrival.Scripts.Utility;

public sealed class MissionScoreBreakdown
{
	public int EnemiesKilled { get; }
	public int UnitsLost { get; }
	public int EnemyKillPoints { get; }
	public int UnitLossPoints { get; }
	public int OutcomePoints { get; }
	public int TotalPoints { get; }

	public MissionScoreBreakdown(
		int enemiesKilled,
		int unitsLost,
		int enemyKillPoints,
		int unitLossPoints,
		int outcomePoints)
	{
		EnemiesKilled = Math.Max(0, enemiesKilled);
		UnitsLost = Math.Max(0, unitsLost);
		EnemyKillPoints = enemyKillPoints;
		UnitLossPoints = unitLossPoints;
		OutcomePoints = outcomePoints;
		TotalPoints = ClampToInt(
			(long)enemyKillPoints + unitLossPoints + outcomePoints);
	}

	private static int ClampToInt(long value) =>
		(int)Math.Clamp(value, int.MinValue, int.MaxValue);
}

public sealed class MissionBattleResult
{
	public Enums.MissionStatus Outcome { get; }
	public MissionScoreBreakdown Score { get; }
	public MissionRecoveryResult Recovery { get; }
	public bool IsQuickBattle { get; }
	public long RecoveryWeightCapacity { get; }
	public string MissionName { get; }

	public long RecoveryWeight => Recovery?.GetRecoveredItemWeight() ?? 0;
	public long SaleProceeds => Recovery?.SaleProceeds ?? 0;
	public bool IsOverCapacity =>
		!IsQuickBattle && RecoveryWeight > RecoveryWeightCapacity;

	public MissionBattleResult(
		Enums.MissionStatus outcome,
		MissionScoreBreakdown score,
		MissionRecoveryResult recovery,
		bool isQuickBattle,
		long recoveryWeightCapacity,
		string missionName)
	{
		Outcome = outcome;
		Score = score ?? throw new ArgumentNullException(nameof(score));
		Recovery = recovery ?? new MissionRecoveryResult();
		IsQuickBattle = isQuickBattle;
		RecoveryWeightCapacity = Math.Max(0, recoveryWeightCapacity);
		MissionName = string.IsNullOrWhiteSpace(missionName)
			? "Mission"
			: missionName;
	}
}
