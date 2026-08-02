using Godot;

/// <summary>
/// Designer-authored data and completion behaviour for a base facility.
/// Derive from this resource and override OnPlaced to add a new facility effect.
/// </summary>
[GlobalClass]
public partial class FacilityDefinition : Resource
{
	[Export] public string FacilityId { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = "Facility";
	[Export(PropertyHint.MultilineText)] public string Purpose { get; set; } = string.Empty;
	[Export(PropertyHint.Range, "0,100000000,1,or_greater")]
	public int InitialCost { get; set; }
	[Export(PropertyHint.Range, "0,10000000,1,or_greater")]
	public int MonthlyCost { get; set; }
	[Export(PropertyHint.Range, "0,1000,1,or_greater")]
	public int ScientistCapacity { get; set; }
	[Export] public Vector2I GridSize { get; set; } = Vector2I.One;
	[Export(PropertyHint.Range, "0,3650,1,or_greater")]
	public int BuildTimeDays { get; set; } = 1;
	[Export] public bool UniquePerBase { get; set; }

	/// <summary>
	/// Called once when this facility finishes construction. The construction
	/// record owns the idempotency flag, so implementations can safely change
	/// persistent base statistics here.
	/// </summary>
	public virtual void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
	}

	public Vector2I GetValidatedGridSize() => new(
		Mathf.Max(1, GridSize.X),
		Mathf.Max(1, GridSize.Y));
}
