using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Save-friendly runtime state for one team's copy of a research project.
/// The ResearchProject resource remains immutable and may be shared by many teams.
/// </summary>
public sealed class ResearchProjectProgress
{
	private readonly HashSet<int> appliedResultIndices = new();

	public string ProjectId { get; private set; } = string.Empty;
	public int InitialPoints { get; private set; }
	public int RemainingPoints { get; private set; }
	public int AssignedScientists { get; private set; }
	public bool IsCompleted { get; private set; }
	public bool ResultsApplied { get; private set; }
	public float CompletionRatio => InitialPoints <= 0
		? (IsCompleted ? 1.0f : 0.0f)
		: Mathf.Clamp(
			1.0f - ((float)RemainingPoints / InitialPoints),
			0.0f,
			1.0f);

	private ResearchProjectProgress()
	{
	}

	internal static ResearchProjectProgress Create(ResearchProject project)
	{
		if (project == null) throw new ArgumentNullException(nameof(project));
		int initialPoints = Math.Max(1, project.TotalResearchPoints);
		return new ResearchProjectProgress
		{
			ProjectId = project.GetStableId(),
			InitialPoints = initialPoints,
			RemainingPoints = initialPoints
		};
	}

	internal void SetAssignedScientists(int count) =>
		AssignedScientists = IsCompleted ? 0 : Math.Max(0, count);

	internal void ApplyPoints(long points)
	{
		if (IsCompleted || points <= 0) return;
		RemainingPoints = points >= RemainingPoints
			? 0
			: RemainingPoints - (int)points;
	}

	internal void Complete()
	{
		RemainingPoints = 0;
		AssignedScientists = 0;
		IsCompleted = true;
	}

	internal bool IsResultApplied(int resultIndex) =>
		ResultsApplied || appliedResultIndices.Contains(resultIndex);

	internal void MarkResultApplied(int resultIndex)
	{
		if (resultIndex >= 0)
			appliedResultIndices.Add(resultIndex);
	}

	internal void UpdateResultsApplied(int resultCount)
	{
		if (resultCount <= 0)
		{
			ResultsApplied = true;
			return;
		}

		for (int resultIndex = 0; resultIndex < resultCount; resultIndex++)
		{
			if (!appliedResultIndices.Contains(resultIndex))
			{
				ResultsApplied = false;
				return;
			}
		}

		ResultsApplied = true;
	}

	public Godot.Collections.Dictionary<string, Variant> Save()
	{
		var savedResultIndices = new Godot.Collections.Array<int>();
		var sortedResultIndices = new List<int>(appliedResultIndices);
		sortedResultIndices.Sort();
		foreach (int resultIndex in sortedResultIndices)
			savedResultIndices.Add(resultIndex);

		return new Godot.Collections.Dictionary<string, Variant>
		{
			["initialPoints"] = InitialPoints,
			["remainingPoints"] = RemainingPoints,
			["assignedScientists"] = AssignedScientists,
			["completed"] = IsCompleted,
			["resultsApplied"] = ResultsApplied,
			["appliedResultIndices"] = savedResultIndices
		};
	}

	internal static ResearchProjectProgress Load(
		string projectId,
		Godot.Collections.Dictionary<string, Variant> data,
		ResearchProject definition)
	{
		int definitionPoints = Math.Max(1, definition?.TotalResearchPoints ?? 1);
		int initialPoints = data.TryGetValue("initialPoints", out Variant initial)
			? Math.Max(1, initial.AsInt32())
			: definitionPoints;
		bool completed = data.TryGetValue("completed", out Variant isCompleted)
			&& isCompleted.AsBool();
		int remainingPoints = data.TryGetValue("remainingPoints", out Variant remaining)
			? Math.Clamp(remaining.AsInt32(), 0, initialPoints)
			: initialPoints;

		// A zero-point save is treated as complete.
		completed |= remainingPoints == 0;
		var progress = new ResearchProjectProgress
		{
			ProjectId = projectId,
			InitialPoints = initialPoints,
			RemainingPoints = completed ? 0 : remainingPoints,
			AssignedScientists = completed
				? 0
				: Math.Max(
					0,
					data.TryGetValue("assignedScientists", out Variant assigned)
						? assigned.AsInt32()
						: 0),
			IsCompleted = completed
		};

		bool hasPerResultState = data.TryGetValue(
			"appliedResultIndices",
			out Variant savedResultIndices) &&
			savedResultIndices.VariantType == Variant.Type.Array;
		bool savedAllResultsApplied = data.TryGetValue(
			"resultsApplied",
			out Variant applied) && applied.AsBool();

		if (completed && !hasPerResultState)
		{
			// Saves from before per-result tracking always treated a completed
			// project's rewards as final. Preserve that behavior rather than
			// granting legacy rewards again during the next research tick.
			progress.ResultsApplied = true;
			return progress;
		}

		if (hasPerResultState)
		{
			foreach (Variant savedIndex in savedResultIndices.AsGodotArray())
			{
				int resultIndex = savedIndex.AsInt32();
				if (resultIndex >= 0)
					progress.appliedResultIndices.Add(resultIndex);
			}
		}

		// Preserve an explicitly finalized state, including a completed project
		// whose definition is temporarily unavailable during load.
		progress.ResultsApplied = completed && savedAllResultsApplied;
		if (completed && !progress.ResultsApplied && definition != null)
		{
			progress.UpdateResultsApplied(
				definition.ResearchResults?.Count ?? 0);
		}

		return progress;
	}
}
