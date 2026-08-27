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
	[Export(PropertyHint.Range, "0,1000000,1,or_greater")]
	public int ItemStorageCapacityBonus { get; set; } = 250;

	public override int GetItemStorageCapacityBonus() =>
		Mathf.Max(0, ItemStorageCapacityBonus);

	public override string GetEffectsSummary() =>
		$"Detection radius: +{Mathf.Max(0, DetectionRadiusBonus):N0}\n" +
		$"Troop capacity: +{Mathf.Max(0, TroopCapacityBonus):N0}\n" +
		$"Scientist capacity: +{Mathf.Max(0, ScientistCapacityBonus):N0}\n" +
		$"Item storage: +{Mathf.Max(0, ItemStorageCapacityBonus):N0}";

	public override void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
		baseDefinition?.AddDetectionRadiusBonus(DetectionRadiusBonus);
		baseDefinition?.AddTroopCapacity(TroopCapacityBonus);
		baseDefinition?.AddScientistCapacity(ScientistCapacityBonus);
		baseDefinition?.AddItemStorageCapacity(
			construction?.ItemStorageCapacityBonus ??
			GetItemStorageCapacityBonus());
		
	}
}
