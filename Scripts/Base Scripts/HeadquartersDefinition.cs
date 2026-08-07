using Godot;
using System;

public partial class HeadquartersDefinition : FacilityDefinition
{
	[Export(PropertyHint.Range, "0,100,1,or_greater")]
	public int DetectionRadiusBonus { get; set; } = 5;
	[Export(PropertyHint.Range, "0,1000,1,or_greater")]
	public int TroopCapacityBonus { get; set; } = 8;

	public int countryOpinionChgange = 25;

	
	[Export(PropertyHint.Range, "0,1000,1,or_greater")]
	public int ScientistCapacityBonus { get; set; } = 8;
	public override void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
		baseDefinition?.AddDetectionRadiusBonus(DetectionRadiusBonus);
		baseDefinition?.AddTroopCapacity(TroopCapacityBonus);
		baseDefinition?.AddScientistCapacity(ScientistCapacityBonus);
		
	}
}
