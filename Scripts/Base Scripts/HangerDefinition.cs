using Godot;
using System;

[GlobalClass]
public partial class HangerDefinition : FacilityDefinition
{
	[Export(PropertyHint.Range, "0,1000,1,or_greater")]
	public int CraftCapacityBonus { get; set; } = 1;

	public override void OnPlaced(
		TeamBaseCellDefinition baseDefinition,
		FacilityConstruction construction)
	{
		baseDefinition?.AddCraftCapacity(CraftCapacityBonus);
	}
}
