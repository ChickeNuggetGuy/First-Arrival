using System;
using System.Collections.Generic;
using Godot;

public enum FacilityConstructionState
{
	Queued,
	UnderConstruction,
	Constructed
}

/// <summary>
/// Save-friendly runtime state for one facility footprint in a base grid.
/// </summary>
public sealed class FacilityConstruction
{
	public string Id { get; private set; } = string.Empty;
	public string FacilityId { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string Purpose { get; private set; } = string.Empty;
	public string DefinitionPath { get; private set; } = string.Empty;
	public string ScenePath { get; private set; } = string.Empty;
	public Vector2I Origin { get; private set; }
	public Vector2I GridSize { get; private set; } = Vector2I.One;
	public int InitialCost { get; private set; }
	public int MonthlyCost { get; private set; }
	public int ScientistCapacity { get; private set; }
	public int BuildTimeDays { get; private set; }
	public int RemainingBuildDays { get; private set; }
	public string AttachedToId { get; private set; } = string.Empty;
	public FacilityConstructionState State { get; private set; }
	public bool EffectsApplied { get; private set; }

	public bool IsConstructed => State == FacilityConstructionState.Constructed;
	public bool IsWaitingForDependency => State == FacilityConstructionState.Queued;

	private FacilityConstruction()
	{
	}

	public static FacilityConstruction Create(
		FacilityDefinition definition,
		Vector2I origin,
		string scenePath,
		string attachedToId = "",
		bool constructImmediately = false)
	{
		if (definition == null)
			throw new ArgumentNullException(nameof(definition));

		int buildDays = Mathf.Max(0, definition.BuildTimeDays);
		return new FacilityConstruction
		{
			Id = Guid.NewGuid().ToString("N"),
			FacilityId = string.IsNullOrWhiteSpace(definition.FacilityId)
				? definition.DisplayName
				: definition.FacilityId,
			DisplayName = definition.DisplayName,
			Purpose = definition.Purpose,
			DefinitionPath = definition.ResourcePath,
			ScenePath = scenePath ?? string.Empty,
			Origin = origin,
			GridSize = definition.GetValidatedGridSize(),
			InitialCost = Mathf.Max(0, definition.InitialCost),
			MonthlyCost = Mathf.Max(0, definition.MonthlyCost),
			ScientistCapacity = Mathf.Max(0, definition.ScientistCapacity),
			BuildTimeDays = buildDays,
			RemainingBuildDays = constructImmediately ? 0 : buildDays,
			AttachedToId = attachedToId ?? string.Empty,
			State = constructImmediately
				? FacilityConstructionState.Constructed
				: string.IsNullOrEmpty(attachedToId)
					? FacilityConstructionState.UnderConstruction
					: FacilityConstructionState.Queued,
			EffectsApplied = false
		};
	}

	public IEnumerable<Vector2I> GetOccupiedCells()
	{
		for (int x = 0; x < GridSize.X; x++)
		{
			for (int z = 0; z < GridSize.Y; z++)
				yield return Origin + new Vector2I(x, z);
		}
	}

	internal void StartConstruction()
	{
		if (State == FacilityConstructionState.Queued)
			State = FacilityConstructionState.UnderConstruction;
	}

	internal bool AdvanceOneDay()
	{
		if (State != FacilityConstructionState.UnderConstruction)
			return false;

		RemainingBuildDays = Mathf.Max(0, RemainingBuildDays - 1);
		if (RemainingBuildDays > 0) return false;

		State = FacilityConstructionState.Constructed;
		return true;
	}

	internal void MarkEffectsApplied() => EffectsApplied = true;

	public Godot.Collections.Dictionary<string, Variant> Save() => new()
	{
		["id"] = Id,
		["facilityId"] = FacilityId,
		["displayName"] = DisplayName,
		["purpose"] = Purpose,
		["definitionPath"] = DefinitionPath,
		["scenePath"] = ScenePath,
		["origin"] = Origin,
		["gridSize"] = GridSize,
		["initialCost"] = InitialCost,
		["monthlyCost"] = MonthlyCost,
		["scientistCapacity"] = ScientistCapacity,
		["buildTimeDays"] = BuildTimeDays,
		["remainingBuildDays"] = RemainingBuildDays,
		["attachedToId"] = AttachedToId,
		["state"] = (int)State,
		["effectsApplied"] = EffectsApplied
	};

	public static FacilityConstruction Load(
		Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data == null) return null;
		bool hasSavedScientistCapacity = data.ContainsKey("scientistCapacity");

		var construction = new FacilityConstruction
		{
			Id = GetString(data, "id", Guid.NewGuid().ToString("N")),
			FacilityId = GetString(data, "facilityId"),
			DisplayName = GetString(data, "displayName", "Facility"),
			Purpose = GetString(data, "purpose"),
			DefinitionPath = GetString(data, "definitionPath"),
			ScenePath = GetString(data, "scenePath"),
			Origin = data.TryGetValue("origin", out Variant origin)
				? origin.AsVector2I()
				: Vector2I.Zero,
			GridSize = data.TryGetValue("gridSize", out Variant size)
				? size.AsVector2I()
				: Vector2I.One,
			InitialCost = GetInt(data, "initialCost"),
			MonthlyCost = GetInt(data, "monthlyCost"),
			ScientistCapacity = GetInt(data, "scientistCapacity"),
			BuildTimeDays = GetInt(data, "buildTimeDays"),
			RemainingBuildDays = GetInt(data, "remainingBuildDays"),
			AttachedToId = GetString(data, "attachedToId"),
			State = (FacilityConstructionState)GetInt(data, "state"),
			EffectsApplied = data.TryGetValue("effectsApplied", out Variant applied)
				&& applied.AsBool()
		};

		if (!hasSavedScientistCapacity &&
			!string.IsNullOrWhiteSpace(construction.DefinitionPath) &&
			ResourceLoader.Exists(construction.DefinitionPath))
		{
			FacilityDefinition definition =
				ResourceLoader.Load<FacilityDefinition>(construction.DefinitionPath);
			if (definition != null)
				construction.ScientistCapacity = definition.ScientistCapacity;
		}

		construction.GridSize = new Vector2I(
			Mathf.Max(1, construction.GridSize.X),
			Mathf.Max(1, construction.GridSize.Y));
		construction.InitialCost = Mathf.Max(0, construction.InitialCost);
		construction.MonthlyCost = Mathf.Max(0, construction.MonthlyCost);
		construction.ScientistCapacity = Mathf.Max(
			0,
			construction.ScientistCapacity);
		construction.BuildTimeDays = Mathf.Max(0, construction.BuildTimeDays);
		construction.RemainingBuildDays = Mathf.Max(
			0,
			construction.RemainingBuildDays);
		return construction;
	}

	private static string GetString(
		Godot.Collections.Dictionary<string, Variant> data,
		string key,
		string fallback = "") =>
		data.TryGetValue(key, out Variant value) ? value.AsString() : fallback;

	private static int GetInt(
		Godot.Collections.Dictionary<string, Variant> data,
		string key,
		int fallback = 0) =>
		data.TryGetValue(key, out Variant value) ? value.AsInt32() : fallback;
}
