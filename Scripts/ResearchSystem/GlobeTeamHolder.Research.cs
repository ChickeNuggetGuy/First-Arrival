using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Inventory_System;
using Godot;

public enum ResearchProjectStatus
{
	Missing,
	Locked,
	Available,
	Active,
	Completed
}

public partial class GlobeTeamHolder
{
	private ResearchDatabase researchDatabase;
	private readonly System.Collections.Generic.Dictionary<
		string,
		ResearchProjectProgress> researchProgress = new(
			StringComparer.OrdinalIgnoreCase);
	private readonly List<string> pendingResearchEvents = new();

	public int HiredScientists { get; private set; }
	public int ScientistCapacity
	{
		get
		{
			long capacity = 0;
			if (Bases == null) return 0;
			foreach (TeamBaseCellDefinition baseDefinition in Bases)
			{
				if (baseDefinition == null) continue;
				capacity += baseDefinition.ScientistCapacity;
				if (capacity >= int.MaxValue) return int.MaxValue;
			}
			return (int)capacity;
		}
	}

	public int AssignedScientists
	{
		get
		{
			long assigned = 0;
			foreach (ResearchProjectProgress progress in researchProgress.Values)
			{
				assigned += progress.AssignedScientists;
				if (assigned >= int.MaxValue) return int.MaxValue;
			}
			return (int)assigned;
		}
	}

	public int UnassignedScientists => Math.Max(
		0,
		Math.Min(HiredScientists, ScientistCapacity) - AssignedScientists);
	public ResearchDatabase ResearchDatabase => researchDatabase;

	[Signal]
	public delegate void ScientistsChangedEventHandler(
		GlobeTeamHolder teamHolder,
		int hiredScientists,
		int scientistCapacity,
		int assignedScientists);
	[Signal]
	public delegate void ResearchProgressChangedEventHandler(
		GlobeTeamHolder teamHolder,
		string projectId,
		int remainingPoints,
		int assignedScientists);
	[Signal]
	public delegate void ResearchProjectCompletedEventHandler(
		GlobeTeamHolder teamHolder,
		string projectId);
	[Signal]
	public delegate void ItemUnlockedEventHandler(
		GlobeTeamHolder teamHolder,
		int itemId);
	[Signal]
	public delegate void ResearchEventTriggeredEventHandler(
		GlobeTeamHolder teamHolder,
		string eventId);

	public void ConfigureResearchDatabase(ResearchDatabase database)
	{
		researchDatabase = database;
		NormalizeResearchAssignments();
	}

	public List<ResearchProject> GetAvailableResearchProjects()
	{
		var available = new List<ResearchProject>();
		if (researchDatabase?.Projects == null) return available;

		foreach (ResearchProject project in researchDatabase.Projects)
		{
			if (IsResearchProjectAvailable(project))
				available.Add(project);
		}
		return available;
	}

	public ResearchProjectStatus GetResearchProjectStatus(string projectId)
	{
		ResearchProject project = researchDatabase?.GetProject(projectId);
		if (project == null) return ResearchProjectStatus.Missing;
		if (IsResearchProjectCompleted(project.GetStableId()))
			return ResearchProjectStatus.Completed;
		if (!IsResearchProjectAvailable(project))
			return ResearchProjectStatus.Locked;
		return researchProgress.TryGetValue(
			project.GetStableId(),
			out ResearchProjectProgress progress) &&
			progress.AssignedScientists > 0
				? ResearchProjectStatus.Active
				: ResearchProjectStatus.Available;
	}

	public bool IsResearchProjectAvailable(string projectId) =>
		IsResearchProjectAvailable(researchDatabase?.GetProject(projectId));

	public bool IsResearchProjectCompleted(string projectId) =>
		!string.IsNullOrWhiteSpace(projectId) &&
		researchProgress.TryGetValue(projectId.Trim(), out ResearchProjectProgress progress) &&
		progress.IsCompleted;

	public bool TryGetResearchProgress(
		string projectId,
		out ResearchProjectProgress progress)
	{
		progress = null;
		ResearchProject project = researchDatabase?.GetProject(projectId);
		if (project == null) return false;
		progress = EnsureResearchProgress(project);
		return true;
	}

	/// <summary>
	/// Reads existing runtime state without starting or snapshotting a project.
	/// Use this for display-only queries so simply opening research does not make
	/// later point-cost balance changes save-significant.
	/// </summary>
	public bool TryGetExistingResearchProgress(
		string projectId,
		out ResearchProjectProgress progress)
	{
		progress = null;
		ResearchProject project = researchDatabase?.GetProject(projectId);
		return project != null && researchProgress.TryGetValue(
			project.GetStableId(),
			out progress);
	}

	public bool TryHireScientists(int count)
	{
		NormalizeResearchAssignments();
		if (count <= 0 || HiredScientists > ScientistCapacity - count)
			return false;

		HiredScientists += count;
		EmitScientistsChanged();
		return true;
	}

	public bool TryDismissScientists(int count)
	{
		NormalizeResearchAssignments();
		// Scientists above current laboratory capacity are inactive, but they are
		// still idle hires and must remain dismissible.
		int idleScientists = Math.Max(0, HiredScientists - AssignedScientists);
		if (count <= 0 || count > idleScientists) return false;
		HiredScientists -= count;
		EmitScientistsChanged();
		return true;
	}

	public bool TryAssignScientists(string projectId, int count)
	{
		NormalizeResearchAssignments();
		if (count <= 0 || count > UnassignedScientists) return false;
		ResearchProject project = researchDatabase?.GetProject(projectId);
		if (!IsResearchProjectAvailable(project)) return false;

		ResearchProjectProgress progress = EnsureResearchProgress(project);
		int maximum = project.MaxAssignedScientists <= 0
			? int.MaxValue
			: project.MaxAssignedScientists;
		if (progress.AssignedScientists > maximum - count) return false;

		progress.SetAssignedScientists(progress.AssignedScientists + count);
		EmitResearchProgressChanged(progress);
		EmitScientistsChanged();
		return true;
	}

	public bool TryUnassignScientists(string projectId, int count)
	{
		NormalizeResearchAssignments();
		if (count <= 0 ||
			!researchProgress.TryGetValue(projectId?.Trim() ?? string.Empty, out var progress) ||
			count > progress.AssignedScientists)
		{
			return false;
		}

		progress.SetAssignedScientists(progress.AssignedScientists - count);
		EmitResearchProgressChanged(progress);
		EmitScientistsChanged();
		return true;
	}

	public bool TrySetScientistAssignment(string projectId, int targetCount)
	{
		NormalizeResearchAssignments();
		if (targetCount < 0) return false;
		if (!TryGetResearchProgress(projectId, out ResearchProjectProgress progress))
			return false;
		int difference = targetCount - progress.AssignedScientists;
		return difference switch
		{
			> 0 => TryAssignScientists(projectId, difference),
			< 0 => TryUnassignScientists(projectId, -difference),
			_ => true
		};
	}

	/// <summary>
	/// Each assigned scientist contributes one point per simulated day.
	/// Scientists released by a completed project remain idle for the rest of a
	/// multi-day jump instead of being silently reassigned.
	/// </summary>
	public void AdvanceResearch(int daysAdvanced)
	{
		if (daysAdvanced <= 0 || researchDatabase?.Projects == null) return;
		NormalizeResearchAssignments();

		foreach (ResearchProject project in researchDatabase.Projects)
		{
			if (project == null ||
				!researchProgress.TryGetValue(
					project.GetStableId(),
					out ResearchProjectProgress progress))
			{
				continue;
			}

			if (progress.IsCompleted)
			{
				ApplyPendingResearchResults(project, progress);
				continue;
			}

			if (
				progress.AssignedScientists <= 0 ||
				!IsResearchProjectAvailable(project))
			{
				continue;
			}

			long points = (long)progress.AssignedScientists * daysAdvanced;
			progress.ApplyPoints(points);
			if (progress.RemainingPoints > 0)
			{
				EmitResearchProgressChanged(progress);
				continue;
			}

			progress.Complete();
			EmitResearchProgressChanged(progress);
			EmitScientistsChanged();
			ApplyPendingResearchResults(project, progress);
			EmitSignal(SignalName.ResearchProjectCompleted, this, project.GetStableId());
		}
	}

	private void ApplyPendingResearchResults(
		ResearchProject project,
		ResearchProjectProgress progress)
	{
		if (project == null || progress == null || progress.ResultsApplied)
			return;

		int resultCount = project.ResearchResults?.Count ?? 0;
		bool anyResultFailed = false;
		for (int resultIndex = 0; resultIndex < resultCount; resultIndex++)
		{
			if (progress.IsResultApplied(resultIndex)) continue;

			ResearchResult result = project.ResearchResults[resultIndex];
			if (result != null && result.TryApply(this, project))
			{
				progress.MarkResultApplied(resultIndex);
				continue;
			}

			anyResultFailed = true;
		}

		progress.UpdateResultsApplied(resultCount);
		if (anyResultFailed)
		{
			GD.PushError(
				$"One or more results for research '{project.GetStableId()}' " +
				"could not be applied and will be retried on a future research tick.");
		}
	}

	public bool IsItemUnlocked(ItemData itemData)
	{
		if (itemData == null) return false;
		string requiredResearch = itemData.RequiredResearch?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(requiredResearch))
		{
			return itemData.AvailableAtCampaignStart ||
				unlockeditemIDArray.Contains(itemData.ItemID);
		}

		// Research-gated content needs both halves of the authoring contract:
		// the named project must be complete, and one of its results must have
		// explicitly granted this item ID.
		return IsResearchProjectCompleted(requiredResearch) &&
			unlockeditemIDArray.Contains(itemData.ItemID);
	}

	public List<int> GetUnlockedItemIdsSnapshot() =>
		new(unlockeditemIDArray);

	public List<string> GetCompletedResearchProjectIdsSnapshot()
	{
		var completedProjectIds = new List<string>();
		foreach (var entry in researchProgress)
		{
			if (entry.Value.IsCompleted)
				completedProjectIds.Add(entry.Key);
		}
		completedProjectIds.Sort(StringComparer.OrdinalIgnoreCase);
		return completedProjectIds;
	}

	public void QueueResearchEvent(string eventId)
	{
		string normalizedId = eventId?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(normalizedId))
		{
			GD.PushError("A research result tried to trigger an event without an EventId.");
			return;
		}

		pendingResearchEvents.Add(normalizedId);
		EmitSignal(SignalName.ResearchEventTriggered, this, normalizedId);
	}

	public bool TryConsumeResearchEvent(out string eventId)
	{
		if (pendingResearchEvents.Count == 0)
		{
			eventId = string.Empty;
			return false;
		}

		eventId = pendingResearchEvents[0];
		pendingResearchEvents.RemoveAt(0);
		return true;
	}

	private bool IsResearchProjectAvailable(ResearchProject project)
	{
		if (project == null || string.IsNullOrEmpty(project.GetStableId()) ||
			IsResearchProjectCompleted(project.GetStableId()))
		{
			return false;
		}

		if (project.Prerequisites == null) return true;
		foreach (ResearchProject prerequisite in project.Prerequisites)
		{
			if (prerequisite == null ||
				!IsResearchProjectCompleted(prerequisite.GetStableId()))
			{
				return false;
			}
		}
		return true;
	}

	private ResearchProjectProgress EnsureResearchProgress(ResearchProject project)
	{
		string projectId = project.GetStableId();
		if (!researchProgress.TryGetValue(projectId, out ResearchProjectProgress progress))
		{
			progress = ResearchProjectProgress.Create(project);
			researchProgress[projectId] = progress;
		}
		return progress;
	}

	private void NormalizeResearchAssignments()
	{
		int remainingScientists = Math.Min(HiredScientists, ScientistCapacity);
		if (researchDatabase?.Projects == null)
		{
			foreach (ResearchProjectProgress progress in researchProgress.Values)
				progress.SetAssignedScientists(0);
			return;
		}

		var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ResearchProject project in researchDatabase.Projects)
		{
			if (project == null) continue;
			string projectId = project.GetStableId();
			knownIds.Add(projectId);
			if (!researchProgress.TryGetValue(projectId, out var progress)) continue;

			int maximum = project.MaxAssignedScientists <= 0
				? int.MaxValue
				: project.MaxAssignedScientists;
			int normalized = progress.IsCompleted ||
				!IsResearchProjectAvailable(project)
					? 0
					: Math.Min(
						progress.AssignedScientists,
						Math.Min(maximum, remainingScientists));
			progress.SetAssignedScientists(normalized);
			remainingScientists -= normalized;
		}

		foreach (var entry in researchProgress)
		{
			if (!knownIds.Contains(entry.Key))
				entry.Value.SetAssignedScientists(0);
		}
	}

	private void EmitScientistsChanged() => EmitSignal(
		SignalName.ScientistsChanged,
		this,
		HiredScientists,
		ScientistCapacity,
		AssignedScientists);

	private void EmitResearchProgressChanged(ResearchProjectProgress progress) =>
		EmitSignal(
			SignalName.ResearchProgressChanged,
			this,
			progress.ProjectId,
			progress.RemainingPoints,
			progress.AssignedScientists);

	private Godot.Collections.Dictionary<string, Variant> SaveResearchState()
	{
		var projects = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var entry in researchProgress)
			projects[entry.Key] = entry.Value.Save();

		var queuedEvents = new Godot.Collections.Array<string>();
		foreach (string eventId in pendingResearchEvents)
			queuedEvents.Add(eventId);

		return new Godot.Collections.Dictionary<string, Variant>
		{
			["version"] = 1,
			["hiredScientists"] = HiredScientists,
			["projects"] = projects,
			["pendingEvents"] = queuedEvents
		};
	}

	private Godot.Collections.Array<int> SaveUnlockedItemIds()
	{
		var unlockedIds = new Godot.Collections.Array<int>();
		var sortedIds = new List<int>(unlockeditemIDArray);
		sortedIds.Sort();
		foreach (int itemId in sortedIds)
			unlockedIds.Add(itemId);
		return unlockedIds;
	}

	private void LoadResearchState(
		Godot.Collections.Dictionary<string, Variant> teamData)
	{
		researchProgress.Clear();
		pendingResearchEvents.Clear();
		HiredScientists = 0;
		if (!teamData.TryGetValue("research", out Variant savedResearch) ||
			savedResearch.VariantType != Variant.Type.Dictionary)
		{
			return;
		}

		var researchData =
			savedResearch.AsGodotDictionary<string, Variant>();
		HiredScientists = researchData.TryGetValue(
			"hiredScientists",
			out Variant hired)
			? Math.Max(0, hired.AsInt32())
			: 0;

		if (researchData.TryGetValue("projects", out Variant savedProjects) &&
			savedProjects.VariantType == Variant.Type.Dictionary)
		{
			foreach (var entry in savedProjects.AsGodotDictionary<string, Variant>())
			{
				if (string.IsNullOrWhiteSpace(entry.Key) ||
					entry.Value.VariantType != Variant.Type.Dictionary)
				{
					continue;
				}

				string projectId = entry.Key.Trim();
				ResearchProject definition = researchDatabase?.GetProject(projectId);
				researchProgress[projectId] = ResearchProjectProgress.Load(
					projectId,
					entry.Value.AsGodotDictionary<string, Variant>(),
					definition);
			}
		}

		if (researchData.TryGetValue("pendingEvents", out Variant savedEvents) &&
			savedEvents.VariantType == Variant.Type.Array)
		{
			foreach (Variant savedEvent in savedEvents.AsGodotArray())
			{
				string eventId = savedEvent.AsString().Trim();
				if (!string.IsNullOrEmpty(eventId))
					pendingResearchEvents.Add(eventId);
			}
		}

		NormalizeResearchAssignments();
	}

	private void LoadUnlockedItemIds(
		Godot.Collections.Dictionary<string, Variant> teamData)
	{
		unlockeditemIDArray.Clear();
		if (!teamData.TryGetValue("unlockedItemIds", out Variant savedItems) ||
			savedItems.VariantType != Variant.Type.Array)
		{
			return;
		}

		foreach (Variant savedItem in savedItems.AsGodotArray())
		{
			int itemId = savedItem.AsInt32();
			if (itemId >= 0 && !unlockeditemIDArray.Contains(itemId))
				unlockeditemIDArray.Add(itemId);
		}
	}
}
