using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class TeamBaseCellDefinition : HexCellDefinition
{
	public Enums.UnitTeam teamAffiliation = Enums.UnitTeam.None;
	public int DetectionRadius { get; set; } = 10;
	public float DetectionChance { get; set; } = 0.35f;
	public bool ShowDetectionRadius { get; set; } = true;

	private Godot.Collections.Array<GridObject> stationedGridObjects = new Godot.Collections.Array<GridObject>();

	private int maxCraft = 3;
	private Godot.Collections.Dictionary<Enums.CraftStatus, Godot.Collections.Array<Craft>> craft = new();

	public Godot.Collections.Dictionary<Enums.CraftStatus, Godot.Collections.Array<Craft>> GetAllCraftData() => craft;
	protected Godot.Collections.Dictionary<int, int> itemCounts = new();

	public List<Craft> CraftList
	{
		get
		{
			List<Craft> uniqueCraft = new List<Craft>();
			foreach (var pair in craft)
			{
				foreach (var craft in pair.Value)
				{
					if (!uniqueCraft.Contains(craft)) uniqueCraft.Add(craft);
				}
			}

			return uniqueCraft;
		}
	}

	public int CraftCount
	{
		get
		{
			List<Craft> uniqueCraft = new List<Craft>();
			foreach (var pair in craft)
			{
				foreach (var craft in pair.Value)
				{
					if (!uniqueCraft.Contains(craft)) uniqueCraft.Add(craft);
				}
			}

			return uniqueCraft.Count;
		}
	}

	public int MaxCraft => maxCraft;

	public TeamBaseCellDefinition(int cellIndex, string name, Enums.UnitTeam team, List<Craft> craftList) : base(
		cellIndex, name, true)
	{
		this.teamAffiliation = team;
		RevealForTeam(team);
		if (craftList != null)
		{
			craft.Add(Enums.CraftStatus.Idle, new Godot.Collections.Array<Craft>());
			craft.Add(Enums.CraftStatus.EnRoute, new Godot.Collections.Array<Craft>());
			craft.Add(Enums.CraftStatus.Home, new Godot.Collections.Array<Craft>());
			foreach (var c in craftList)
			{
				craft[c.Status].Add(c);
			}
		}
		else
		{
			craft.Add(Enums.CraftStatus.Idle, new Godot.Collections.Array<Craft>());
			craft.Add(Enums.CraftStatus.EnRoute, new Godot.Collections.Array<Craft>());
			craft.Add(Enums.CraftStatus.Home, new Godot.Collections.Array<Craft>());
		}
	}


	public Godot.Collections.Array<GridObject> GetStationedGridObjects() =>
		stationedGridObjects;

	public bool TryAddStationedGridObject(GridObject gridObject)
	{
		if (gridObject == null) return false;
		if (stationedGridObjects.Contains(gridObject)) return false;

		gridObject.SetIsActive(false);
		gridObject.Visible = false;
		stationedGridObjects.Add(gridObject);
		return true;
	}

	public bool TryRemoveStationedGridObject(GridObject gridObject)
	{
		if (gridObject == null) return false;
		if (!stationedGridObjects.Contains(gridObject)) return false;

		stationedGridObjects.Remove(gridObject);
		return true;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = base.Save();

		var serializedCrafts =
			new Godot.Collections.Array<
				Godot.Collections.Dictionary<string, Variant>>();

		foreach (var c in CraftList)
		{
			if (c == null) continue;
			serializedCrafts.Add(c.Save());
		}

		data["crafts"] = serializedCrafts;
		data["units"] = GridObjectSerializationUtility.SaveGridObjects(
			stationedGridObjects
		);
		data["teamAffiliation"] = (int)teamAffiliation;
		data["itemCounts"] = itemCounts;
		data["detectionRadius"] = DetectionRadius;
		data["detectionChance"] = DetectionChance;
		data["showDetectionRadius"] = ShowDetectionRadius;

		return data;
	}

	public async Task LoadAsync(
		Godot.Collections.Dictionary<string, Variant> data,
		Node unitParent
	)
	{
		base.Load(data);

		if (data.ContainsKey("teamAffiliation"))
		{
			teamAffiliation =
				(Enums.UnitTeam)data["teamAffiliation"].AsInt32();
		}

		if (data.ContainsKey("detectionRadius"))
			DetectionRadius = data["detectionRadius"].AsInt32();
		if (data.ContainsKey("detectionChance"))
			DetectionChance = data["detectionChance"].AsSingle();
		if (data.ContainsKey("showDetectionRadius"))
			ShowDetectionRadius = data["showDetectionRadius"].AsBool();

		craft.Clear();
		craft.Add(Enums.CraftStatus.Idle, new Godot.Collections.Array<Craft>());
		craft.Add(
			Enums.CraftStatus.EnRoute,
			new Godot.Collections.Array<Craft>()
		);
		craft.Add(Enums.CraftStatus.Home, new Godot.Collections.Array<Craft>());

		itemCounts.Clear();
		if (data.ContainsKey("itemCounts"))
		{
			var rawItemCounts = data["itemCounts"].AsGodotDictionary();

			foreach (Variant key in rawItemCounts.Keys)
			{
				int itemId = key.AsInt32();
				int count = rawItemCounts[key].AsInt32();
				itemCounts[itemId] = count;
			}
		}

		stationedGridObjects.Clear();
		if (data.ContainsKey("units"))
		{
			var unitsArray =
				data["units"]
					.AsGodotArray<
						Godot.Collections.Dictionary<string, Variant>>();

			stationedGridObjects =
				await GridObjectSerializationUtility.LoadGridObjectsAsync(
					unitsArray,
					unitParent,
					true
				);
		}

		if (data.ContainsKey("crafts"))
		{
			var craftsArray =
				data["crafts"]
					.AsGodotArray<
						Godot.Collections.Dictionary<string, Variant>>();

			foreach (var craftData in craftsArray)
			{
				if (craftData == null) continue;

				int itemID = craftData.ContainsKey("itemID")
					? craftData["itemID"].AsInt32()
					: -1;

				if (itemID == -1)
				{
					GD.PrintErr("Craft data missing itemID, skipping.");
					continue;
				}

				ItemData originalData = InventoryManager.Instance.GetItemData(
					itemID
				);

				if (originalData is not Craft craftResource)
				{
					GD.PrintErr(
						$"ItemID {itemID} did not resolve to a Craft " +
						$"resource."
					);
					continue;
				}

				Craft newInstance = (Craft)craftResource.Duplicate(true);

				await newInstance.LoadAsync(craftData, unitParent);
				newInstance.Setup(newInstance.Index, cellIndex, this);

				if (!craft.ContainsKey(newInstance.Status))
				{
					craft.Add(
						newInstance.Status,
						new Godot.Collections.Array<Craft>()
					);
				}

				craft[newInstance.Status].Add(newInstance);
			}
		}
	}

	#region Craft Functions

	private void AddCraft(Enums.CraftStatus status, Craft craftToAdd)
	{
		craftToAdd.Setup(CraftCount, cellIndex, this);
		craft[status].Add(craftToAdd);
	}

	public bool TryAddCraft(Enums.CraftStatus status, Craft craftToAdd)
	{
		if (craftToAdd == null) return false;
		if (!craft.ContainsKey(status)) return false;
		if (craft[status].Contains(craftToAdd)) return false;

		GlobeTeamHolder team = GlobeTeamManager.Instance.GetTeamData(teamAffiliation);
		if (team == null) return false;
		if (CraftCount >= maxCraft) return false;

		if (!team.TryRemoveFunds(craftToAdd.buyPrice)) return false;
		AddCraft(status, craftToAdd);
		return true;
	}

	public bool TryAddCraftWithoutPurchase(Enums.CraftStatus status, Craft craftToAdd)
	{
		if (craftToAdd == null) return false;
		if (!craft.ContainsKey(status)) return false;
		if (craft[status].Contains(craftToAdd)) return false;
		if (CraftCount >= maxCraft) return false;

		AddCraft(status, craftToAdd);
		return true;
	}

	private void RemoveCraft(Enums.CraftStatus status, Craft craftToRemove)
	{
		craft[status].Remove(craftToRemove);

		if (craftToRemove.visual != null)
		{
			craftToRemove.visual.QueueFree();
		}
	}

	public bool TryRemoveCraft(Enums.CraftStatus status, Craft craftToRemove)
	{
		if (craftToRemove == null) return false;
		if (!craft.ContainsKey(status)) return false;
		if (!craft[status].Contains(craftToRemove)) return false;

		RemoveCraft(status, craftToRemove);
		return true;
	}

	public int GetCraftCountForItem(int itemId)
	{
		int count = 0;
		foreach (Craft existingCraft in CraftList)
		{
			if (existingCraft.ItemID == itemId) count++;
		}
		return count;
	}

	public int GetSellableCraftCountForItem(int itemId)
	{
		int count = 0;
		foreach (Craft existingCraft in CraftList)
		{
			if (existingCraft.ItemID == itemId &&
			    existingCraft.Status != Enums.CraftStatus.EnRoute &&
			    existingCraft.GetStationedUnits().Count == 0)
				count++;
		}
		return count;
	}

	public bool TryRemoveCraftByItemId(int itemId)
	{
		foreach (Craft existingCraft in CraftList)
		{
			if (existingCraft.ItemID == itemId &&
			    existingCraft.Status != Enums.CraftStatus.EnRoute &&
			    existingCraft.GetStationedUnits().Count == 0)
				return TryRemoveCraft(existingCraft.Status, existingCraft);
		}

		return false;
	}


	public bool TryGetCraftFromIndex(int index, out Craft oraft)
	{
		foreach (Craft c in CraftList)
		{
			if (c.Index == index)
			{
				oraft = c;
				return true;
			}
		}

		oraft = null;
		return false;
	}


	public bool TryChangeCraftStatus(Enums.CraftStatus newStatus, Craft craft)
	{
		if (newStatus == Enums.CraftStatus.None || craft == null) return false;
		if (!this.craft.ContainsKey(newStatus)) return false;

		if (!this.craft.ContainsKey(craft.Status)) return false;
		if (!this.craft[craft.Status].Contains(craft)) return false;

		this.craft[craft.Status].Remove(craft);
		this.craft[newStatus].Add(craft);
		craft.Status = newStatus;

		return true;
	}


	public async Task<bool> SendCraft(
		int startCellIndex,
		int targetCellIndex,
		Craft craft,
		GlobeTeamManager teamManager,
		bool interactWithMission = true,
		Action<Craft> onArrived = null)
	{
		// Early out: craft is already at its home base 
		if (startCellIndex == targetCellIndex && targetCellIndex == craft.HomeBaseIndex)
		{
			// Move craft directly into Home status
			TryChangeCraftStatus(Enums.CraftStatus.Home, craft);
			craft.TargetCellIndex = -1;

			// Clean up the visual
			if (craft.visual != null)
			{
				craft.visual.QueueFree();
				craft.SetVisual(null);
			}

			onArrived?.Invoke(craft);
			return true;
		}

		GlobePathfinder pathfinder = GlobePathfinder.Instance;
		GlobeHexGridManager manager = GlobeHexGridManager.Instance;
		GlobeMissionManager missionManager = GlobeMissionManager.Instance;

		if (pathfinder == null || manager == null ||
		    (interactWithMission && missionManager == null))
		{
			GD.PrintErr("manager is null");
			return false;
		}

		// Only the destination must be land. The pathfinder remains unrestricted,
		// allowing aircraft to cross water cells en route.
		if (!manager.GetCellFromIndex(targetCellIndex, excludeWater: true).HasValue)
		{
			GD.PrintErr($"Cannot send craft to water cell {targetCellIndex}.");
			return false;
		}

		List<int> path = pathfinder.GetPath(startCellIndex, targetCellIndex);

		if (path == null || path.Count == 0)
		{
			GD.PrintErr("No path found for mission!");
			return false;
		}

		if (!TryChangeCraftStatus(Enums.CraftStatus.EnRoute, craft))
		{
			GD.PrintErr("Failed to change craft status!");
			return false;
		}

		MissionCellDefinition missionCellDefinition = null;
		if (interactWithMission &&
		    missionManager.GetActiveMissions().ContainsKey(targetCellIndex))
		{
			MissionCellDefinition possibleMission = missionManager.GetActiveMissions()[targetCellIndex];
			if (!possibleMission.missionStatus.HasFlag(Enums.MissionStatus.Visited))
				missionCellDefinition = possibleMission;
		}

		TeamBaseCellDefinition teamBaseCellDefinition = null;
		teamManager.GetTeamData(teamAffiliation)?.TryGetBaseAtIndex(
			targetCellIndex,
			out teamBaseCellDefinition);

		if (teamManager.shipScene == null)
		{
			GD.PrintErr("Cannot send craft: ship scene is not configured.");
			TryChangeCraftStatus(Enums.CraftStatus.Idle, craft);
			return false;
		}

		MeshInstance3D shipNode = craft.visual
		                          ?? teamManager.shipScene.Instantiate<MeshInstance3D>();

		if (craft.visual == null)
			craft.SetVisual(shipNode);
		shipNode.Visible = teamAffiliation == teamManager.ViewingTeam ||
		                   craft.IsVisibleTo(teamManager.ViewingTeam);

		if (teamManager.shipContainer != null)
		{
			if (shipNode.GetParent() == null)
				teamManager.shipContainer.AddChild(shipNode);
			else if (shipNode.GetParent() != teamManager.shipContainer)
				shipNode.Reparent(teamManager.shipContainer);
		}
		else if (shipNode.GetParent() == null)
			teamManager.AddChild(shipNode);

		var startCell = manager.GetCellFromIndex(path[0]);
		if (startCell.HasValue)
		{
			shipNode.GlobalPosition = startCell.Value.Center;
			DetectionRadiusVisualizer.AttachOrUpdate(
				shipNode,
				startCell.Value.Index,
				craft.DetectionRadius,
				new Color(0.2f, 0.75f, 1.0f, 0.22f),
				craft.ShowDetectionRadius
			);
		}

		craft.TargetCellIndex = targetCellIndex;
		Tween shipTween = teamManager.GetTree().CreateTween();
		List<float> segmentDurations = CalculateFlightSegmentDurations(
			path,
			manager,
			craft
		);
		GlobeTimeManager timeManager = GlobeTimeManager.Instance;

		void ApplyGlobeTimeSpeed(int speed)
		{
			if (!GodotObject.IsInstanceValid(shipTween)) return;
			if (speed <= 0)
			{
				shipTween.Pause();
				return;
			}

			shipTween.SetSpeedScale(speed);
			shipTween.Play();
		}

		if (timeManager != null)
		{
			timeManager.TimeSpeedChanged += ApplyGlobeTimeSpeed;
			ApplyGlobeTimeSpeed(timeManager.GetTimeSpeed());
		}

		if (missionCellDefinition != null)
			missionCellDefinition.SetOnRouteCraft(craft);

		for (int i = 1; i < path.Count; i++)
		{
			HexCellData? cell = manager.GetCellFromIndex(path[i]);
			if (!cell.HasValue)
				continue;

			Vector3 targetPos = cell.Value.Center;
			int reachedCellIndex = cell.Value.Index;

			shipTween.TweenCallback(
				Callable.From(() =>
				{
					Vector3 upDir = shipNode.GlobalPosition.Normalized();
					shipNode.LookAt(targetPos, upDir);
				})
			);

			shipTween.TweenProperty(
				shipNode,
				"global_position",
				targetPos,
				segmentDurations[i - 1]
			).SetTrans(Tween.TransitionType.Linear);
			shipTween.TweenCallback(
				Callable.From(() =>
				{
					craft.CurrentCellIndex = reachedCellIndex;
					DetectionRadiusVisualizer.AttachOrUpdate(
						shipNode,
						reachedCellIndex,
						craft.DetectionRadius,
						new Color(0.2f, 0.75f, 1.0f, 0.22f),
						craft.ShowDetectionRadius
					);
					teamManager.ScanForDefinitions(
						teamAffiliation,
						reachedCellIndex,
						craft.DetectionRadius,
						craft.DetectionChance
					);
					teamManager.TryDetectHostileCraft(
						craft,
						teamAffiliation,
						reachedCellIndex);
				})
			);
		}

		try
		{
			await teamManager.ToSignal(shipTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (timeManager != null && GodotObject.IsInstanceValid(timeManager))
				timeManager.TimeSpeedChanged -= ApplyGlobeTimeSpeed;
		}

		craft.CurrentCellIndex = targetCellIndex;

		if (missionCellDefinition != null)
		{
			// Arrived at a mission – go idle and launch the battle
			TryChangeCraftStatus(Enums.CraftStatus.Idle, craft);

			missionManager.LoadMissionScene(missionCellDefinition);
		}
		else if (teamBaseCellDefinition != null)
		{
			if (teamBaseCellDefinition.cellIndex == craft.HomeBaseIndex)
			{
				// Arrived home
				TryChangeCraftStatus(Enums.CraftStatus.Home, craft);
				craft.TargetCellIndex = -1;

				// Hide the visual now that the craft is docked
				shipNode.QueueFree();
				craft.SetVisual(null);
			}
			else
			{
				// Arrived at a different base – transfer ownership
				TryChangeCraftStatus(Enums.CraftStatus.Idle, craft);
				TryTransferCraft(
					craft.GetBaseCellDefinition(),
					teamBaseCellDefinition,
					new List<Craft>() { craft },
					teamManager
				);
			}
		}
		else
		{
			// Arrived at a plain cell
			TryChangeCraftStatus(Enums.CraftStatus.Idle, craft);
		}

		onArrived?.Invoke(craft);
		return true;
	}

	/// <summary>
	/// Calculates each path segment's duration from a rest-to-rest flight
	/// profile. A craft accelerates to its maximum speed when there is enough
	/// distance, cruises if necessary, then decelerates for arrival. This keeps
	/// both MaxSpeed and Acceleration meaningful for every route length.
	/// </summary>
	private static List<float> CalculateFlightSegmentDurations(
		List<int> path,
		GlobeHexGridManager gridManager,
		Craft craft)
	{
		const float FallbackSecondsPerStep = 0.4f;
		const float MinimumDuration = 0.001f;
		const float DistanceEpsilon = 0.0001f;

		int segmentCount = Math.Max(0, path.Count - 1);
		List<float> segmentDistances = new(segmentCount);
		for (int i = 1; i < path.Count; i++)
		{
			HexCellData? fromCell = gridManager.GetCellFromIndex(path[i - 1]);
			HexCellData? toCell = gridManager.GetCellFromIndex(path[i]);
			segmentDistances.Add(
				fromCell.HasValue && toCell.HasValue
					? fromCell.Value.Center.DistanceTo(toCell.Value.Center)
					: 0f
			);
		}

		float maxSpeed = craft.GetGlobeMaxSpeed();
		if (maxSpeed <= DistanceEpsilon)
			return CreateFallbackSegmentDurations(segmentCount, FallbackSecondsPerStep);

		float totalDistance = 0f;
		foreach (float segmentDistance in segmentDistances)
			totalDistance += segmentDistance;

		if (totalDistance <= DistanceEpsilon)
			return CreateFallbackSegmentDurations(segmentCount, MinimumDuration);

		float acceleration = craft.GetGlobeAcceleration();
		if (acceleration <= DistanceEpsilon)
		{
			List<float> constantSpeedDurations = new(segmentCount);
			foreach (float segmentDistance in segmentDistances)
			{
				constantSpeedDurations.Add(
					Mathf.Max(MinimumDuration, segmentDistance / maxSpeed)
				);
			}
			return constantSpeedDurations;
		}

		float distanceToReachMaxSpeed =
			(maxSpeed * maxSpeed) / (2f * acceleration);
		float accelerationDistance = Mathf.Min(
			distanceToReachMaxSpeed,
			totalDistance / 2f
		);
		float peakSpeed = Mathf.Sqrt(2f * acceleration * accelerationDistance);
		float cruiseDistance = totalDistance - (2f * accelerationDistance);
		float accelerationTime = peakSpeed / acceleration;
		float totalTime = (2f * accelerationTime) +
			(cruiseDistance / peakSpeed);

		List<float> durations = new(segmentCount);
		float distanceTravelled = 0f;
		float previousTime = 0f;
		foreach (float segmentDistance in segmentDistances)
		{
			distanceTravelled += segmentDistance;
			float currentTime = GetFlightTimeAtDistance(
				distanceTravelled,
				totalDistance,
				accelerationDistance,
				cruiseDistance,
				acceleration,
				peakSpeed,
				accelerationTime,
				totalTime
			);
			durations.Add(Mathf.Max(MinimumDuration, currentTime - previousTime));
			previousTime = currentTime;
		}

		return durations;
	}

	private static List<float> CreateFallbackSegmentDurations(
		int segmentCount,
		float duration)
	{
		List<float> durations = new(segmentCount);
		for (int i = 0; i < segmentCount; i++)
			durations.Add(duration);
		return durations;
	}

	private static float GetFlightTimeAtDistance(
		float distanceTravelled,
		float totalDistance,
		float accelerationDistance,
		float cruiseDistance,
		float acceleration,
		float peakSpeed,
		float accelerationTime,
		float totalTime)
	{
		if (distanceTravelled <= accelerationDistance)
			return Mathf.Sqrt((2f * distanceTravelled) / acceleration);

		float cruiseEndDistance = accelerationDistance + cruiseDistance;
		if (distanceTravelled <= cruiseEndDistance)
		{
			return accelerationTime +
				((distanceTravelled - accelerationDistance) / peakSpeed);
		}

		float remainingDistance = Mathf.Max(0f, totalDistance - distanceTravelled);
		return totalTime - Mathf.Sqrt((2f * remainingDistance) / acceleration);
	}


	public static bool TryTransferCraft(TeamBaseCellDefinition fromBase, TeamBaseCellDefinition toBase,
		List<Craft> craftList, GlobeTeamManager teamManager)
	{
		if (fromBase == null || toBase == null)
		{
			GD.PrintErr("Either from/to base is null");
			return false;
		}

		if (craftList == null || craftList.Count == 0)
		{
			GD.PrintErr("Craft list is empty");
			return false;
		}

		if (toBase.maxCraft < craftList.Count + toBase.CraftCount)
		{
			GD.PrintErr("To base cannot take more than total craft!");
			return false;
		}


		bool success = true;
		foreach (Craft craft in craftList)
		{
			if (!fromBase.TryRemoveCraft(craft.Status, craft))
			{
				GD.PrintErr("Failed to remove craft!");
				continue;
			}
			else
			{
				if (!toBase.TryAddCraftWithoutPurchase(craft.Status, craft))
				{
					fromBase.AddCraft(craft.Status, craft);
					success = false;
					continue;
				}
			}
		}

		GD.Print($"Craft transfer success: {success}");
		return success;
	}

	#endregion

	#region Get/Set Funtions

	public Godot.Collections.Dictionary<int, int> GetItemCounts => itemCounts;
	public void SetItemCounts(Godot.Collections.Dictionary<int, int> itemCounts) => this.itemCounts = itemCounts;

	private void AddItem(int itemID, int count)
	{
		if (itemCounts.ContainsKey(itemID))
		{
			itemCounts[itemID] += count;
		}
		else
		{
			itemCounts.Add(itemID, count);
		}
	}


	public bool TryAddItem(int itemID, int count)
	{
		if (InventoryManager.Instance.GetItemData(itemID) == null)
			return false;

		if (count <= 0) return false;

		AddItem(itemID, count);
		return true;
	}


	private void RemoveItem(int itemID, int count)
	{
		if (itemCounts.ContainsKey(itemID))
		{
			if (itemCounts[itemID] >= count)
			{
				itemCounts[itemID] -= count;
			}
			else
			{
				itemCounts.Remove(itemID);
			}
		}
	}


	public bool TryRemoveItem(int itemID, int count)
	{
		if (InventoryManager.Instance.GetItemData(itemID) == null)
			return false;

		if (count <= 0) return false;
		if (!itemCounts.ContainsKey(itemID) || itemCounts[itemID] < count)
			return false;

		RemoveItem(itemID, count);
		if (itemCounts.GetValueOrDefault(itemID, 0) == 0)
			itemCounts.Remove(itemID);
		return true;
	}

	#endregion
}
