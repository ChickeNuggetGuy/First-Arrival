using Godot;

[GlobalClass]
public partial class RadarFacilityDefinition : FacilityDefinition
{
	[Export(PropertyHint.Range, "0,100,1,or_greater")]
	public int DetectionRadiusBonus { get; set; } = 5;

	public override void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
		baseDefinition?.AddDetectionRadiusBonus(DetectionRadiusBonus);
	}
}
