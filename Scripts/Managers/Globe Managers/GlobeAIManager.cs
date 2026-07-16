using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

/// <summary>
/// Runs the alien globe strategy on coarse game-time ticks. The manager chooses
/// operations daily and advances their preparation/travel phases hourly; it
/// intentionally performs no strategic work in _Process.
/// </summary>
[GlobalClass]
public partial class GlobeAIManager : Manager<GlobeAIManager>
{
	public enum AlienGoalType
	{
		AttackCity,
		BuildBase,
		Scan,
	}

	public enum AlienOperationStage
	{
		Preparing,
		Travelling,
		MissionActive,
		Returning,
	}

	private sealed class AlienOperation
	{
		public int Id;
		public AlienGoalType Goal;
		public AlienOperationStage Stage;
		public int SourceBaseCellIndex;
		public int TargetCellIndex;
		public int HoursRemaining;
		public int Difficulty;
		public int CraftHomeBaseIndex = -1;
		public int CraftIndex = -1;
		public int[] Waypoints = Array.Empty<int>();
		public int WaypointIndex;
		public bool PendingSuccess;

		public Godot.Collections.Dictionary<string, Variant> Save() => new()
		{
			["id"] = Id,
			["goal"] = (int)Goal,
			["stage"] = (int)Stage,
			["sourceBaseCellIndex"] = SourceBaseCellIndex,
			["targetCellIndex"] = TargetCellIndex,
			["hoursRemaining"] = HoursRemaining,
			["difficulty"] = Difficulty,
			["craftHomeBaseIndex"] = CraftHomeBaseIndex,
			["craftIndex"] = CraftIndex,
			["waypoints"] = Waypoints,
			["waypointIndex"] = WaypointIndex,
			["pendingSuccess"] = PendingSuccess,
		};

		public static AlienOperation Load(
			Godot.Collections.Dictionary<string, Variant> data)
		{
			if (data == null || !data.ContainsKey("id") ||
			    !data.ContainsKey("targetCellIndex"))
				return null;

			return new AlienOperation
			{
				Id = data["id"].AsInt32(),
				Goal = data.ContainsKey("goal")
					? (AlienGoalType)data["goal"].AsInt32()
					: AlienGoalType.AttackCity,
				Stage = data.ContainsKey("stage")
					? (AlienOperationStage)data["stage"].AsInt32()
					: AlienOperationStage.Preparing,
				SourceBaseCellIndex = data.ContainsKey("sourceBaseCellIndex")
					? data["sourceBaseCellIndex"].AsInt32()
					: -1,
				TargetCellIndex = data["targetCellIndex"].AsInt32(),
				HoursRemaining = data.ContainsKey("hoursRemaining")
					? data["hoursRemaining"].AsInt32()
					: 0,
				Difficulty = data.ContainsKey("difficulty")
					? data["difficulty"].AsInt32()
					: 1,
				CraftHomeBaseIndex = data.ContainsKey("craftHomeBaseIndex")
					? data["craftHomeBaseIndex"].AsInt32()
					: -1,
				CraftIndex = data.ContainsKey("craftIndex")
					? data["craftIndex"].AsInt32()
					: -1,
				Waypoints = data.ContainsKey("waypoints")
					? data["waypoints"].AsInt32Array()
					: Array.Empty<int>(),
				WaypointIndex = data.ContainsKey("waypointIndex")
					? data["waypointIndex"].AsInt32()
					: 0,
				PendingSuccess = data.ContainsKey("pendingSuccess") &&
				                 data["pendingSuccess"].AsBool(),
			};
		}
	}

	private readonly record struct GoalCandidate(
		AlienGoalType Goal,
		int SourceBaseCellIndex,
		int TargetCellIndex,
		int TravelHours,
		float Utility,
		int CraftIndex,
		int[] Waypoints);

	private readonly record struct CraftAssignment(
		TeamBaseCellDefinition Base,
		Craft Craft);

	[Export] private bool aiEnabled = true;
	[Export(PropertyHint.Range, "1,8,1")] private int maxConcurrentOperations = 2;
	[Export(PropertyHint.Range, "0,30,1")] private int daysBetweenDecisions = 1;
	[Export(PropertyHint.Range, "1,72,1")] private int preparationHours = 6;
	[Export(PropertyHint.Range, "0.25,10,0.25")] private float travelWorldUnitsPerHour = 2f;
	[Export(PropertyHint.Range, "1,64,1")] private int cityCandidateSampleSize = 12;
	[Export(PropertyHint.Range, "1,10,1")] private int desiredAlienBases = 2;
	[Export] private int expansionBaseCost = 250000;
	[Export(PropertyHint.Range, "0,100,1")] private float aggression = 50f;
	[Export(PropertyHint.ResourceType, "Craft")] private Craft alienCraftTemplate;
	[Export(PropertyHint.Range, "1,3,1")] private int startingCraftPerBase = 2;
	[Export(PropertyHint.Range, "1,8,1")] private int scanWaypointCount = 4;
	[Export(PropertyHint.Range, "2,64,1")] private int scanRangeSteps = 24;
	[Export(PropertyHint.Range, "0,100,1")] private float scanUtility = 45f;

	private readonly List<AlienOperation> _operations = new();
	private int _nextOperationId = 1;
	private int _daysUntilNextDecision;
	private int _strategicProgress;
	private bool _timeSignalsConnected;

	public int StrategicProgress => _strategicProgress;
	public int ActiveOperationCount => _operations.Count;

	[Signal]
	public delegate void OperationStartedEventHandler(
		int operationId,
		int goal,
		int targetCellIndex);

	[Signal]
	public delegate void OperationResolvedEventHandler(
		int operationId,
		bool alienSucceeded);

	public override string GetManagerName() => "GlobeAIManager";

	protected override Task _Setup(bool loadingData)
	{
		if (!loadingData || !HasLoadedData)
		{
			_operations.Clear();
			_nextOperationId = 1;
			_daysUntilNextDecision = 0;
			_strategicProgress = 0;
		}

		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		GlobeTeamHolder alienTeam = GlobeTeamManager.Instance?.GetTeamData(
			Enums.UnitTeam.Enemy);
		if (alienTeam != null)
		{
			int minimumCraft = loadingData ? 1 : startingCraftPerBase;
			EnsureAlienCraftSupply(alienTeam, minimumCraft);
			RepairMissingCraftAssignments(alienTeam);
		}

		ConnectTimeSignals();
		ResumeInterruptedOperations();

		// Start a new campaign promptly. Loaded campaigns retain their saved
		// cooldown and operations.
		if (aiEnabled && !loadingData && _operations.Count == 0)
			TryPlanOperation();

		return Task.CompletedTask;
	}

	private void ConnectTimeSignals()
	{
		if (_timeSignalsConnected || GlobeTimeManager.Instance == null) return;

		GlobeTimeManager.Instance.HourChanged += OnHourChanged;
		GlobeTimeManager.Instance.DayChanged += OnDayChanged;
		_timeSignalsConnected = true;
	}

	private void OnDayChanged(int dayOfYear, int dayOfMonth, Enums.Day day)
	{
		if (!aiEnabled) return;

		if (_daysUntilNextDecision > 0)
			_daysUntilNextDecision--;

		if (_daysUntilNextDecision <= 0)
			TryPlanOperation();
	}

	private void OnHourChanged(int hour)
	{
		if (!aiEnabled) return;

		// Iterate backwards because a failed launch can remove an operation.
		for (int i = _operations.Count - 1; i >= 0; i--)
		{
			AlienOperation operation = _operations[i];
			if (operation.Stage != AlienOperationStage.Preparing) continue;

			operation.HoursRemaining = Math.Max(0, operation.HoursRemaining - 1);
			if (operation.HoursRemaining > 0) continue;

			DispatchOperation(operation);
		}
	}

	private void TryPlanOperation()
	{
		if (_operations.Count >= maxConcurrentOperations)
		{
			_daysUntilNextDecision = Math.Max(1, daysBetweenDecisions);
			return;
		}

		GlobeTeamHolder alienTeam = GlobeTeamManager.Instance?.GetTeamData(
			Enums.UnitTeam.Enemy);
		if (alienTeam?.Bases == null || alienTeam.Bases.Count == 0)
		{
			_daysUntilNextDecision = 1;
			return;
		}

		List<GoalCandidate> candidates = new();
		AddCityAttackCandidates(candidates, alienTeam);
		AddExpansionCandidate(candidates, alienTeam);
		AddScanCandidate(candidates, alienTeam);

		if (candidates.Count == 0)
		{
			_daysUntilNextDecision = 1;
			return;
		}

		GoalCandidate selected = candidates.MaxBy(candidate => candidate.Utility);
		_operations.Add(new AlienOperation
		{
			Id = _nextOperationId++,
			Goal = selected.Goal,
			Stage = AlienOperationStage.Preparing,
			SourceBaseCellIndex = selected.SourceBaseCellIndex,
			TargetCellIndex = selected.TargetCellIndex,
			HoursRemaining = Math.Max(1, preparationHours),
			Difficulty = Math.Clamp(
				(GlobeMissionManager.Instance?.GlobalDifficulty ?? 1)
				+ (_strategicProgress / 20),
				1,
				10),
			CraftHomeBaseIndex = selected.SourceBaseCellIndex,
			CraftIndex = selected.CraftIndex,
			Waypoints = selected.Waypoints ?? Array.Empty<int>(),
		});

		AlienOperation started = _operations[^1];
		_daysUntilNextDecision = Math.Max(1, daysBetweenDecisions);
		EmitSignal(
			SignalName.OperationStarted,
			started.Id,
			(int)started.Goal,
			started.TargetCellIndex);

		if (DebugMode)
			GD.Print(
				$"[GlobeAI] Started {started.Goal} operation {started.Id} " +
				$"against cell {started.TargetCellIndex}.");
	}

	private void AddCityAttackCandidates(
		List<GoalCandidate> candidates,
		GlobeTeamHolder alienTeam)
	{
		int[] cityCells = GlobeCityManager.Instance?.GetCityCellIndices();
		if (cityCells == null || cityCells.Length == 0) return;

		HashSet<int> reservedTargets = _operations
			.Select(operation => operation.TargetCellIndex)
			.ToHashSet();

		int samples = Math.Min(cityCandidateSampleSize, cityCells.Length);
		for (int i = 0; i < samples; i++)
		{
			int targetCellIndex = cityCells[GD.RandRange(0, cityCells.Length - 1)];
			if (reservedTargets.Contains(targetCellIndex)) continue;
			if (GlobeMissionManager.Instance?.GetActiveMissions()
					.ContainsKey(targetCellIndex) == true)
				continue;

			CraftAssignment? assignment = FindNearestAvailableCraft(
				alienTeam.Bases,
				targetCellIndex);
			if (!assignment.HasValue) continue;

			int travelHours = CalculateTravelHours(
				assignment.Value.Base.cellIndex,
				targetCellIndex);
			float utility = 55f + (aggression * 0.35f)
			                + (_strategicProgress * 0.15f)
			                - (travelHours * 1.25f)
			                - (GetActiveGoalCount(AlienGoalType.AttackCity) * 20f)
			                + (float)GD.RandRange(-8.0, 8.0);

			candidates.Add(new GoalCandidate(
				AlienGoalType.AttackCity,
				assignment.Value.Base.cellIndex,
				targetCellIndex,
				travelHours,
				utility,
				assignment.Value.Craft.Index,
				Array.Empty<int>()));
		}
	}

	private void AddExpansionCandidate(
		List<GoalCandidate> candidates,
		GlobeTeamHolder alienTeam)
	{
		if (alienTeam.Bases.Count >= desiredAlienBases ||
		    !alienTeam.CanAffordCost(expansionBaseCost))
			return;

		HexCellData? target = FindExpansionCell(alienTeam);
		if (!target.HasValue) return;

		CraftAssignment? assignment = FindNearestAvailableCraft(
			alienTeam.Bases,
			target.Value.Index);
		if (!assignment.HasValue) return;

		int travelHours = CalculateTravelHours(
			assignment.Value.Base.cellIndex,
			target.Value.Index);
		float need = desiredAlienBases - alienTeam.Bases.Count;
		float utility = 35f + (need * 35f) - travelHours
		                - (GetActiveGoalCount(AlienGoalType.BuildBase) * 20f)
		                + (float)GD.RandRange(-6.0, 6.0);

		candidates.Add(new GoalCandidate(
			AlienGoalType.BuildBase,
			assignment.Value.Base.cellIndex,
			target.Value.Index,
			travelHours,
			utility,
			assignment.Value.Craft.Index,
			Array.Empty<int>()));
	}

	private void AddScanCandidate(
		List<GoalCandidate> candidates,
		GlobeTeamHolder alienTeam)
	{
		List<CraftAssignment> available = GetAvailableCraftAssignments(alienTeam.Bases);
		if (available.Count == 0) return;

		CraftAssignment assignment = available[GD.RandRange(0, available.Count - 1)];
		int[] waypoints = GenerateScanWaypoints(assignment.Base.cellIndex);
		if (waypoints.Length == 0) return;

		float utility = scanUtility
		                - (GetActiveGoalCount(AlienGoalType.Scan) * 18f)
		                + (float)GD.RandRange(-10.0, 10.0);
		candidates.Add(new GoalCandidate(
			AlienGoalType.Scan,
			assignment.Base.cellIndex,
			waypoints[0],
			CalculateTravelHours(assignment.Base.cellIndex, waypoints[0]),
			utility,
			assignment.Craft.Index,
			waypoints));
	}

	private int[] GenerateScanWaypoints(int sourceCellIndex)
	{
		int anchorCellIndex = sourceCellIndex;
		GlobeTeamHolder playerTeam = GlobeTeamManager.Instance?.GetTeamData(
			Enums.UnitTeam.Player);
		if (playerTeam?.Bases != null && playerTeam.Bases.Count > 0)
		{
			TeamBaseCellDefinition playerBase = playerTeam.Bases[
				GD.RandRange(0, playerTeam.Bases.Count - 1)];
			anchorCellIndex = playerBase.cellIndex;
		}

		HexCellData? anchor = GlobeHexGridManager.Instance?.GetCellFromIndex(
			anchorCellIndex);
		if (!anchor.HasValue) return Array.Empty<int>();

		List<HexCellData> cells = GlobeHexGridManager.Instance.GetCellsInStepRange(
			anchor.Value,
			scanRangeSteps,
			excludeWater: true);
		cells.RemoveAll(cell => cell.Index == sourceCellIndex);

		List<int> waypoints = new();
		while (cells.Count > 0 && waypoints.Count < scanWaypointCount)
		{
			int selection = GD.RandRange(0, cells.Count - 1);
			waypoints.Add(cells[selection].Index);
			cells.RemoveAt(selection);
		}

		return waypoints.ToArray();
	}

	private HexCellData? FindExpansionCell(GlobeTeamHolder alienTeam)
	{
		GlobeHexGridManager grid = GlobeHexGridManager.Instance;
		GlobeTeamManager teams = GlobeTeamManager.Instance;
		if (grid == null || teams == null) return null;

		HashSet<int> occupied = new();
		int[] cityCells = GlobeCityManager.Instance?.GetCityCellIndices();
		if (cityCells != null)
		{
			foreach (int cityCellIndex in cityCells)
				occupied.Add(cityCellIndex);
		}

		foreach (GlobeTeamHolder holder in teams.GetAllTeamData().Values)
		{
			if (holder?.Bases == null) continue;
			foreach (TeamBaseCellDefinition baseDefinition in holder.Bases)
				occupied.Add(baseDefinition.cellIndex);
		}

		foreach (AlienOperation operation in _operations)
			occupied.Add(operation.TargetCellIndex);

		HexCellData? best = null;
		float bestSeparation = float.MinValue;
		for (int i = 0; i < 24; i++)
		{
			HexCellData? candidate = grid.GetRandomCell(excludeWater: true);
			if (!candidate.HasValue || occupied.Contains(candidate.Value.Index)) continue;

			float nearestBaseDistance = float.MaxValue;
			foreach (TeamBaseCellDefinition baseDefinition in alienTeam.Bases)
			{
				HexCellData? baseCell = grid.GetCellFromIndex(baseDefinition.cellIndex);
				if (!baseCell.HasValue) continue;
				nearestBaseDistance = Math.Min(
					nearestBaseDistance,
					baseCell.Value.Center.DistanceTo(candidate.Value.Center));
			}

			if (nearestBaseDistance <= bestSeparation) continue;
			best = candidate;
			bestSeparation = nearestBaseDistance;
		}

		return best;
	}

	private int GetActiveGoalCount(AlienGoalType goal)
		=> _operations.Count(operation => operation.Goal == goal);

	private CraftAssignment? FindNearestAvailableCraft(
		IEnumerable<TeamBaseCellDefinition> bases,
		int targetCellIndex)
	{
		GlobeHexGridManager grid = GlobeHexGridManager.Instance;
		HexCellData? target = grid?.GetCellFromIndex(targetCellIndex);
		if (!target.HasValue) return null;

		CraftAssignment? best = null;
		float bestDistance = float.MaxValue;
		foreach (CraftAssignment assignment in GetAvailableCraftAssignments(bases))
		{
			HexCellData? baseCell = grid.GetCellFromIndex(assignment.Base.cellIndex);
			if (!baseCell.HasValue) continue;

			float distance = baseCell.Value.Center.DistanceSquaredTo(
				target.Value.Center);
			if (distance >= bestDistance) continue;
			bestDistance = distance;
			best = assignment;
		}

		return best;
	}

	private List<CraftAssignment> GetAvailableCraftAssignments(
		IEnumerable<TeamBaseCellDefinition> bases)
	{
		List<CraftAssignment> available = new();
		foreach (TeamBaseCellDefinition baseDefinition in bases)
		{
			foreach (Craft craft in baseDefinition.CraftList)
			{
				bool docked = craft.Status == Enums.CraftStatus.Home ||
				              (craft.Status == Enums.CraftStatus.Idle &&
				               craft.CurrentCellIndex == craft.HomeBaseIndex);
				if (!docked || IsCraftAssigned(craft)) continue;
				available.Add(new CraftAssignment(baseDefinition, craft));
			}
		}

		return available;
	}

	private int CalculateTravelHours(int sourceCellIndex, int targetCellIndex)
	{
		GlobeHexGridManager grid = GlobeHexGridManager.Instance;
		HexCellData? source = grid?.GetCellFromIndex(sourceCellIndex);
		HexCellData? target = grid?.GetCellFromIndex(targetCellIndex);
		if (!source.HasValue || !target.HasValue) return 1;

		float distance = source.Value.Center.DistanceTo(target.Value.Center);
		return Math.Max(
			1,
			Mathf.CeilToInt(distance / Math.Max(0.25f, travelWorldUnitsPerHour)));
	}

	private void DispatchOperation(AlienOperation operation)
	{
		int destination = operation.Goal == AlienGoalType.Scan &&
		                  operation.Waypoints.Length > 0
			? operation.Waypoints[Math.Clamp(
				operation.WaypointIndex,
				0,
				operation.Waypoints.Length - 1)]
			: operation.TargetCellIndex;

		operation.TargetCellIndex = destination;
		operation.Stage = AlienOperationStage.Travelling;
		if (DebugMode)
			GD.Print(
				$"[GlobeAI] Dispatching craft {operation.CraftHomeBaseIndex}:" +
				$"{operation.CraftIndex} for operation {operation.Id} to {destination}.");
		_ = SendOperationCraft(operation, destination);
	}

	private async Task SendOperationCraft(
		AlienOperation operation,
		int destination)
	{
		Craft craft = GetOperationCraft(operation);
		TeamBaseCellDefinition homeBase = craft?.GetBaseCellDefinition();
		if (craft == null || homeBase == null)
		{
			RemoveOperation(operation, alienSucceeded: false);
			return;
		}

		bool sent = await homeBase.SendCraft(
			craft.CurrentCellIndex,
			destination,
			craft,
			GlobeTeamManager.Instance,
			interactWithMission: false,
			onArrived: OnAlienCraftArrived);

		if (!sent && _operations.Contains(operation))
			RemoveOperation(operation, alienSucceeded: false);
	}

	public void OnAlienCraftArrived(Craft craft)
	{
		if (craft == null) return;
		AlienOperation operation = _operations.FirstOrDefault(candidate =>
			candidate.CraftHomeBaseIndex == craft.HomeBaseIndex &&
			candidate.CraftIndex == craft.Index);
		if (operation == null) return;
		if (DebugMode)
			GD.Print(
				$"[GlobeAI] Craft {craft.HomeBaseIndex}:{craft.Index} arrived " +
				$"for operation {operation.Id} ({operation.Goal}).");

		if (operation.Stage == AlienOperationStage.Returning)
		{
			RemoveOperation(operation, operation.PendingSuccess);
			return;
		}

		if (operation.Stage != AlienOperationStage.Travelling) return;

		switch (operation.Goal)
		{
			case AlienGoalType.AttackCity:
				bool missionCreated = GlobeMissionManager.Instance?.TryCreateAlienMission(
					operation.Id,
					operation.TargetCellIndex,
					Enums.MissionType.Eliminate,
					operation.Difficulty) == true;

				if (missionCreated)
				{
					operation.Stage = AlienOperationStage.MissionActive;
					return;
				}

				BeginReturn(operation, alienSucceeded: false);
				break;

			case AlienGoalType.BuildBase:
				HexCellData? target = GlobeHexGridManager.Instance?.GetCellFromIndex(
					operation.TargetCellIndex,
					excludeWater: true);
				GlobeTeamHolder alienTeam = GlobeTeamManager.Instance?.GetTeamData(
					Enums.UnitTeam.Enemy);
				bool built = target.HasValue && alienTeam != null &&
				             GlobeTeamManager.Instance.TryBuildBase(
					             Enums.UnitTeam.Enemy,
					             target.Value,
					             alienTeam.Bases.Count + 1,
					             expansionBaseCost);
				if (built)
				{
					_strategicProgress += 5;
					if (alienTeam.TryGetBaseAtIndex(
						operation.TargetCellIndex,
						out TeamBaseCellDefinition newBase))
						EnsureCraftAtBase(newBase, 1);
				}
				BeginReturn(operation, built);
				break;

			case AlienGoalType.Scan:
				operation.WaypointIndex++;
				if (operation.WaypointIndex < operation.Waypoints.Length)
				{
					operation.TargetCellIndex = operation.Waypoints[operation.WaypointIndex];
					_ = SendOperationCraft(operation, operation.TargetCellIndex);
					return;
				}

				BeginReturn(operation, alienSucceeded: true);
				break;
		}
	}

	private void BeginReturn(AlienOperation operation, bool alienSucceeded)
	{
		Craft craft = GetOperationCraft(operation);
		if (craft == null)
		{
			RemoveOperation(operation, alienSucceeded);
			return;
		}

		operation.PendingSuccess = alienSucceeded;
		operation.Stage = AlienOperationStage.Returning;
		operation.TargetCellIndex = craft.HomeBaseIndex;
		_ = SendOperationCraft(operation, craft.HomeBaseIndex);
	}

	/// <summary>
	/// Called by GlobeMissionManager when a mission tied to an alien operation
	/// resolves. A player failure or timeout is an alien strategic success.
	/// </summary>
	public void ResolveOperation(int operationId, Enums.MissionStatus outcome)
	{
		if (operationId < 0) return;

		AlienOperation operation = _operations.FirstOrDefault(
			candidate => candidate.Id == operationId);
		if (operation == null) return;

		bool alienSucceeded = outcome == Enums.MissionStatus.Failed ||
		                      outcome == Enums.MissionStatus.Timeout;
		if (alienSucceeded)
			_strategicProgress += 10;

		BeginReturn(operation, alienSucceeded);
	}

	public bool IsCraftAssigned(Craft craft)
	{
		if (craft == null) return false;
		return _operations.Any(operation =>
			operation.CraftHomeBaseIndex == craft.HomeBaseIndex &&
			operation.CraftIndex == craft.Index);
	}

	private Craft GetOperationCraft(AlienOperation operation)
	{
		GlobeTeamHolder alienTeam = GlobeTeamManager.Instance?.GetTeamData(
			Enums.UnitTeam.Enemy);
		if (alienTeam?.Bases == null) return null;

		foreach (TeamBaseCellDefinition baseDefinition in alienTeam.Bases)
		{
			if (baseDefinition.cellIndex != operation.CraftHomeBaseIndex) continue;
			if (baseDefinition.TryGetCraftFromIndex(operation.CraftIndex, out Craft craft))
				return craft;
		}

		return null;
	}

	private void EnsureAlienCraftSupply(GlobeTeamHolder alienTeam, int perBase)
	{
		if (alienTeam?.Bases == null) return;
		foreach (TeamBaseCellDefinition baseDefinition in alienTeam.Bases)
			EnsureCraftAtBase(baseDefinition, perBase);
	}

	private void EnsureCraftAtBase(TeamBaseCellDefinition baseDefinition, int minimum)
	{
		if (baseDefinition == null) return;
		alienCraftTemplate ??= ResourceLoader.Load<Craft>(
			"res://Data/Items/Aien_Craft_Item.tres");
		if (alienCraftTemplate == null) return;

		while (baseDefinition.CraftCount < Math.Min(minimum, baseDefinition.MaxCraft))
		{
			Craft craft = (Craft)alienCraftTemplate.Duplicate(true);
			craft.Status = Enums.CraftStatus.Home;
			craft.CurrentCellIndex = -1;
			craft.HomeBaseIndex = -1;
			craft.TargetCellIndex = -1;
			if (!baseDefinition.TryAddCraftWithoutPurchase(
				Enums.CraftStatus.Home,
				craft))
				break;
		}
	}

	private void RepairMissingCraftAssignments(GlobeTeamHolder alienTeam)
	{
		foreach (AlienOperation operation in _operations)
		{
			if (GetOperationCraft(operation) != null) continue;
			CraftAssignment? assignment = FindNearestAvailableCraft(
				alienTeam.Bases,
				operation.TargetCellIndex);
			if (!assignment.HasValue) continue;

			operation.CraftHomeBaseIndex = assignment.Value.Base.cellIndex;
			operation.CraftIndex = assignment.Value.Craft.Index;
			operation.SourceBaseCellIndex = assignment.Value.Base.cellIndex;
			if (operation.Stage != AlienOperationStage.MissionActive)
			{
				operation.Stage = AlienOperationStage.Preparing;
				operation.HoursRemaining = Math.Max(1, preparationHours);
			}
		}
	}

	private void ResumeInterruptedOperations()
	{
		foreach (AlienOperation operation in _operations.ToArray())
		{
			if (operation.Stage != AlienOperationStage.Travelling &&
			    operation.Stage != AlienOperationStage.Returning)
				continue;

			Craft craft = GetOperationCraft(operation);
			if (craft == null || craft.Status == Enums.CraftStatus.EnRoute) continue;

			if (craft.CurrentCellIndex == operation.TargetCellIndex)
				OnAlienCraftArrived(craft);
			else
				_ = SendOperationCraft(operation, operation.TargetCellIndex);
		}
	}

	private void RemoveOperation(AlienOperation operation, bool alienSucceeded)
	{
		if (!_operations.Remove(operation)) return;
		EmitSignal(SignalName.OperationResolved, operation.Id, alienSucceeded);

		if (DebugMode)
			GD.Print(
				$"[GlobeAI] Operation {operation.Id} resolved. " +
				$"Alien success: {alienSucceeded}.");
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var operationData = new Godot.Collections.Array<
			Godot.Collections.Dictionary<string, Variant>>();
		foreach (AlienOperation operation in _operations)
			operationData.Add(operation.Save());

		return new Godot.Collections.Dictionary<string, Variant>
		{
			["nextOperationId"] = _nextOperationId,
			["daysUntilNextDecision"] = _daysUntilNextDecision,
			["strategicProgress"] = _strategicProgress,
			["operations"] = operationData,
		};
	}

	public override Task Load(
		Godot.Collections.Dictionary<string, Variant> data)
	{
		_operations.Clear();
		if (!HasLoadedData || data == null) return Task.CompletedTask;

		_nextOperationId = data.ContainsKey("nextOperationId")
			? Math.Max(1, data["nextOperationId"].AsInt32())
			: 1;
		_daysUntilNextDecision = data.ContainsKey("daysUntilNextDecision")
			? Math.Max(0, data["daysUntilNextDecision"].AsInt32())
			: 0;
		_strategicProgress = data.ContainsKey("strategicProgress")
			? Math.Max(0, data["strategicProgress"].AsInt32())
			: 0;

		if (!data.ContainsKey("operations")) return Task.CompletedTask;
		var operationData = data["operations"].AsGodotArray<
			Godot.Collections.Dictionary<string, Variant>>();
		foreach (var savedOperation in operationData)
		{
			AlienOperation operation = AlienOperation.Load(savedOperation);
			if (operation != null)
				_operations.Add(operation);
		}

		return Task.CompletedTask;
	}

	public override void Deinitialize()
	{
		if (_timeSignalsConnected && GlobeTimeManager.Instance != null)
		{
			GlobeTimeManager.Instance.HourChanged -= OnHourChanged;
			GlobeTimeManager.Instance.DayChanged -= OnDayChanged;
		}

		_timeSignalsConnected = false;
	}
}
