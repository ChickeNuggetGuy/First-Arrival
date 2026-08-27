using Godot;
using System;
using FirstArrival.Scripts.Utility;

public abstract partial class MissionBase(
	string name,
	string description,
	Enums.MissionType missionType,
	int difficulty,
	int enemySpawnCount,
	int cellIndex,
	Enums.MissionRecoveryType recoveryType) : Resource
{
	public const int DefaultPointsPerEnemyKilled = 25;
	public const int DefaultPointsPerUnitLost = -50;
	public const int DefaultSuccessfulMissionPoints = 325;
	public const int DefaultFailedMissionPoints = -250;
	public const int DefaultAbortedMissionPoints = 0;
	public const int DefaultTimeoutMissionPoints = -200;

	public string missionName = name;
	public string missionDescription = description;
	public int missionDifficulty = difficulty;
	public Enums.MissionType MissionType = missionType;
	public Enums.MissionRecoveryType RecoveryType = recoveryType;
	public int cellIndex = cellIndex;
	public int PointsPerEnemyKilled { get; private set; } =
		DefaultPointsPerEnemyKilled;
	public int PointsPerUnitLost { get; private set; } =
		DefaultPointsPerUnitLost;
	public int SuccessfulMissionPoints { get; private set; } =
		DefaultSuccessfulMissionPoints;
	public int FailedMissionPoints { get; private set; } =
		DefaultFailedMissionPoints;
	public int AbortedMissionPoints { get; private set; } =
		DefaultAbortedMissionPoints;
	public int TimeoutMissionPoints { get; private set; } =
		DefaultTimeoutMissionPoints;

	public int EnemySpawnCount = enemySpawnCount;

	public MissionScoreBreakdown CalculateScore(
		int enemiesKilled,
		int unitsLost,
		Enums.MissionStatus outcome)
	{
		int killPoints = MultiplyAndClamp(enemiesKilled, PointsPerEnemyKilled);
		int lossPoints = MultiplyAndClamp(unitsLost, PointsPerUnitLost);
		return new MissionScoreBreakdown(
			enemiesKilled,
			unitsLost,
			killPoints,
			lossPoints,
			GetOutcomePoints(outcome));
	}

	public static MissionScoreBreakdown CalculateDefaultScore(
		int enemiesKilled,
		int unitsLost,
		Enums.MissionStatus outcome)
	{
		int killPoints = MultiplyAndClamp(
			enemiesKilled,
			DefaultPointsPerEnemyKilled);
		int lossPoints = MultiplyAndClamp(
			unitsLost,
			DefaultPointsPerUnitLost);
		int outcomePoints = outcome switch
		{
			Enums.MissionStatus.Successful => DefaultSuccessfulMissionPoints,
			Enums.MissionStatus.Failed => DefaultFailedMissionPoints,
			Enums.MissionStatus.Aborted => DefaultAbortedMissionPoints,
			Enums.MissionStatus.Timeout => DefaultTimeoutMissionPoints,
			_ => 0
		};
		return new MissionScoreBreakdown(
			enemiesKilled,
			unitsLost,
			killPoints,
			lossPoints,
			outcomePoints);
	}

	public int GetOutcomePoints(Enums.MissionStatus outcome) => outcome switch
	{
		Enums.MissionStatus.Successful => SuccessfulMissionPoints,
		Enums.MissionStatus.Failed => FailedMissionPoints,
		Enums.MissionStatus.Aborted => AbortedMissionPoints,
		Enums.MissionStatus.Timeout => TimeoutMissionPoints,
		_ => 0
	};

	public void RestoreScoring(
		Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data == null) return;
		if (data.ContainsKey("pointsPerEnemyKilled"))
			PointsPerEnemyKilled = data["pointsPerEnemyKilled"].AsInt32();
		if (data.ContainsKey("pointsPerUnitLost"))
			PointsPerUnitLost = data["pointsPerUnitLost"].AsInt32();
		if (data.ContainsKey("successfulMissionPoints"))
			SuccessfulMissionPoints = data["successfulMissionPoints"].AsInt32();
		if (data.ContainsKey("failedMissionPoints"))
			FailedMissionPoints = data["failedMissionPoints"].AsInt32();
		if (data.ContainsKey("abortedMissionPoints"))
			AbortedMissionPoints = data["abortedMissionPoints"].AsInt32();
		if (data.ContainsKey("timeoutMissionPoints"))
			TimeoutMissionPoints = data["timeoutMissionPoints"].AsInt32();
	}

	private static int MultiplyAndClamp(int count, int points) =>
		(int)Math.Clamp(
			(long)Math.Max(0, count) * points,
			int.MinValue,
			int.MaxValue);
	
	public virtual Godot.Collections.Dictionary<string, Variant> Save()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "type", (int)MissionType },
			{ "recoveryType", (int)RecoveryType },
			{"name", missionName },
			{"description", missionDescription },
			{"difficulty", missionDifficulty },
			{ "enemyCount", EnemySpawnCount },
			{ "pointsPerEnemyKilled", PointsPerEnemyKilled },
			{ "pointsPerUnitLost", PointsPerUnitLost },
			{ "successfulMissionPoints", SuccessfulMissionPoints },
			{ "failedMissionPoints", FailedMissionPoints },
			{ "abortedMissionPoints", AbortedMissionPoints },
			{ "timeoutMissionPoints", TimeoutMissionPoints }
		};
	}
}
