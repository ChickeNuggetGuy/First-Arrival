using Godot;

[GlobalClass]
public partial class ItemStoresFacilityDefinition : FacilityDefinition
{
	[Export(PropertyHint.Range, "0,1000000,1,or_greater")]
	public int ItemStorageCapacityBonus { get; set; } = 1000;

	public override int GetItemStorageCapacityBonus() =>
		Mathf.Max(0, ItemStorageCapacityBonus);

	public override string GetEffectsSummary() =>
		$"Item storage: +{Mathf.Max(0, ItemStorageCapacityBonus):N0}";

	public override void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
		baseDefinition?.AddItemStorageCapacity(
			construction?.ItemStorageCapacityBonus ??
			GetItemStorageCapacityBonus());
	}
}
