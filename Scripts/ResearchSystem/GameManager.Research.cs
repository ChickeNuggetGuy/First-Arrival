using System.Collections.Generic;
using FirstArrival.Scripts.Inventory_System;
using Godot;

namespace FirstArrival.Scripts.Managers;

public partial class GameManager
{
	private readonly HashSet<int> currentTeamUnlockedItemIds = new();
	private readonly HashSet<string> currentTeamCompletedResearchIds = new(
		System.StringComparer.OrdinalIgnoreCase);
	private bool openResearchOnNextGlobe;

	public void RequestResearchWindowOnGlobe() =>
		openResearchOnNextGlobe = true;

	public void CancelResearchWindowRequest() =>
		openResearchOnNextGlobe = false;

	public bool ConsumeResearchWindowRequest()
	{
		bool requested = openResearchOnNextGlobe;
		openResearchOnNextGlobe = false;
		return requested;
	}

	public void SetCurrentTeamResearchState(
		IEnumerable<int> itemIds,
		IEnumerable<string> completedProjectIds)
	{
		currentTeamUnlockedItemIds.Clear();
		if (itemIds != null)
		{
			foreach (int itemId in itemIds)
			{
				if (itemId >= 0)
					currentTeamUnlockedItemIds.Add(itemId);
			}
		}

		currentTeamCompletedResearchIds.Clear();
		if (completedProjectIds == null) return;
		foreach (string projectId in completedProjectIds)
		{
			string normalizedId = projectId?.Trim() ?? string.Empty;
			if (!string.IsNullOrEmpty(normalizedId))
				currentTeamCompletedResearchIds.Add(normalizedId);
		}
	}

	public bool IsItemUnlocked(ItemData itemData)
	{
		if (itemData == null) return false;
		string requiredResearch = itemData.RequiredResearch?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(requiredResearch))
		{
			return itemData.AvailableAtCampaignStart ||
				currentTeamUnlockedItemIds.Contains(itemData.ItemID);
		}

		return currentTeamCompletedResearchIds.Contains(requiredResearch) &&
			currentTeamUnlockedItemIds.Contains(itemData.ItemID);
	}

	private Godot.Collections.Array<int> SaveCurrentTeamUnlockedItems()
	{
		var savedIds = new Godot.Collections.Array<int>();
		var sortedIds = new List<int>(currentTeamUnlockedItemIds);
		sortedIds.Sort();
		foreach (int itemId in sortedIds)
			savedIds.Add(itemId);
		return savedIds;
	}

	private Godot.Collections.Array<string> SaveCurrentTeamCompletedResearch()
	{
		var savedIds = new Godot.Collections.Array<string>();
		var sortedIds = new List<string>(currentTeamCompletedResearchIds);
		sortedIds.Sort(System.StringComparer.OrdinalIgnoreCase);
		foreach (string projectId in sortedIds)
			savedIds.Add(projectId);
		return savedIds;
	}

	private void LoadCurrentTeamUnlockedItems(
		Godot.Collections.Dictionary<string, Variant> data)
	{
		currentTeamUnlockedItemIds.Clear();
		if (!data.TryGetValue("currentTeamUnlockedItemIds", out Variant savedIds) ||
			savedIds.VariantType != Variant.Type.Array)
		{
			return;
		}

		foreach (Variant savedId in savedIds.AsGodotArray())
		{
			int itemId = savedId.AsInt32();
			if (itemId >= 0)
				currentTeamUnlockedItemIds.Add(itemId);
		}
	}

	private void LoadCurrentTeamCompletedResearch(
		Godot.Collections.Dictionary<string, Variant> data)
	{
		currentTeamCompletedResearchIds.Clear();
		if (!data.TryGetValue(
			"currentTeamCompletedResearchIds",
			out Variant savedIds) ||
			savedIds.VariantType != Variant.Type.Array)
		{
			return;
		}

		foreach (Variant savedId in savedIds.AsGodotArray())
		{
			string projectId = savedId.AsString().Trim();
			if (!string.IsNullOrEmpty(projectId))
				currentTeamCompletedResearchIds.Add(projectId);
		}
	}
}
