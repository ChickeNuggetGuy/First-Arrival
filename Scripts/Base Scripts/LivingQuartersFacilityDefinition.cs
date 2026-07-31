using Godot;

[GlobalClass]
public partial class LivingQuartersFacilityDefinition : FacilityDefinition
{
	[Export(PropertyHint.Range, "0,1000,1,or_greater")]
	public int TroopCapacityBonus { get; set; } = 8;

	public override void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
		baseDefinition?.AddTroopCapacity(TroopCapacityBonus);
	}
}
