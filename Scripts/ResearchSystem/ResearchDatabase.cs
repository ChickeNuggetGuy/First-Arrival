using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class ResearchDatabase : Resource
{
	[Export] public Array<ResearchProject> Projects { get; private set; } = new();

	public ResearchProject GetProject(string projectId)
	{
		if (string.IsNullOrWhiteSpace(projectId) || Projects == null)
			return null;

		foreach (ResearchProject project in Projects)
		{
			if (project != null && project.GetStableId().Equals(
				projectId.Trim(),
				StringComparison.OrdinalIgnoreCase))
			{
				return project;
			}
		}

		return null;
	}

	public List<string> GetValidationErrors()
	{
		var errors = new List<string>();
		var projectsById = new System.Collections.Generic.Dictionary<
			string,
			ResearchProject>(StringComparer.OrdinalIgnoreCase);

		if (Projects == null)
		{
			errors.Add("The research database has no project array.");
			return errors;
		}
		if (Projects.Count == 0)
		{
			errors.Add("The research database contains no projects.");
			return errors;
		}

		for (int i = 0; i < Projects.Count; i++)
		{
			ResearchProject project = Projects[i];
			if (project == null)
			{
				errors.Add($"Project entry {i} is empty.");
				continue;
			}

			string projectId = project.GetStableId();
			if (string.IsNullOrEmpty(projectId))
			{
				errors.Add($"Project entry {i} has no stable ProjectId.");
				continue;
			}

			if (!projectsById.TryAdd(projectId, project))
				errors.Add($"ProjectId '{projectId}' is used more than once.");
			if (project.TotalResearchPoints <= 0)
				errors.Add($"Project '{projectId}' must require at least one point.");
		}

		foreach (var entry in projectsById)
		{
			ResearchProject project = entry.Value;
			if (project.Prerequisites != null)
			{
				foreach (ResearchProject prerequisite in project.Prerequisites)
				{
					if (prerequisite == null)
					{
						errors.Add($"Project '{entry.Key}' has an empty prerequisite.");
						continue;
					}

					string prerequisiteId = prerequisite.GetStableId();
					if (entry.Key.Equals(
						prerequisiteId,
						StringComparison.OrdinalIgnoreCase))
					{
						errors.Add($"Project '{entry.Key}' requires itself.");
					}
					else if (!projectsById.ContainsKey(prerequisiteId))
					{
						errors.Add(
							$"Project '{entry.Key}' requires '{prerequisiteId}', " +
							"which is not in the database.");
					}
				}
			}

			if (project.ResearchResults == null) continue;
			for (int i = 0; i < project.ResearchResults.Count; i++)
			{
				ResearchResult result = project.ResearchResults[i];
				if (result == null)
				{
					errors.Add($"Project '{entry.Key}' has an empty result at index {i}.");
					continue;
				}

				if (result is not UnlockItemsResult unlockResult) continue;
				if (unlockResult.UnlockedItems == null ||
					unlockResult.UnlockedItems.Count == 0)
				{
					errors.Add(
						$"Project '{entry.Key}' has an item-unlock result with no items.");
					continue;
				}

				for (int itemIndex = 0;
					 itemIndex < unlockResult.UnlockedItems.Count;
					 itemIndex++)
				{
					var item = unlockResult.UnlockedItems[itemIndex];
					if (item == null)
					{
						errors.Add(
							$"Project '{entry.Key}' has an empty unlocked item at " +
							$"result {i}, item {itemIndex}.");
						continue;
					}

					string requiredResearch =
						item.RequiredResearch?.Trim() ?? string.Empty;
					if (!string.IsNullOrEmpty(requiredResearch) &&
						!entry.Key.Equals(
							requiredResearch,
							StringComparison.OrdinalIgnoreCase))
					{
						errors.Add(
							$"Project '{entry.Key}' unlocks '{item.ItemName}', but the " +
							$"item requires research '{requiredResearch}'.");
					}
				}
			}
		}

		FindDependencyCycles(projectsById, errors);
		return errors;
	}

	public bool ValidateAndReport()
	{
		List<string> errors = GetValidationErrors();
		foreach (string error in errors)
			GD.PushError($"Research database: {error}");
		return errors.Count == 0;
	}

	private static void FindDependencyCycles(
		System.Collections.Generic.Dictionary<string, ResearchProject> projectsById,
		List<string> errors)
	{
		var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (string projectId in projectsById.Keys)
			Visit(projectId, projectsById, visiting, visited, reported, errors);
	}

	private static void Visit(
		string projectId,
		System.Collections.Generic.Dictionary<string, ResearchProject> projectsById,
		HashSet<string> visiting,
		HashSet<string> visited,
		HashSet<string> reported,
		List<string> errors)
	{
		if (visited.Contains(projectId)) return;
		if (!visiting.Add(projectId))
		{
			if (reported.Add(projectId))
				errors.Add($"The prerequisite graph contains a cycle at '{projectId}'.");
			return;
		}

		ResearchProject project = projectsById[projectId];
		if (project.Prerequisites != null)
		{
			foreach (ResearchProject prerequisite in project.Prerequisites)
			{
				string prerequisiteId = prerequisite?.GetStableId() ?? string.Empty;
				if (projectsById.ContainsKey(prerequisiteId))
				{
					Visit(
						prerequisiteId,
						projectsById,
						visiting,
						visited,
						reported,
						errors);
				}
			}
		}

		visiting.Remove(projectId);
		visited.Add(projectId);
	}
}
