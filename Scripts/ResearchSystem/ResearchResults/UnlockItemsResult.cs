using Godot;
using FirstArrival.Scripts.Inventory_System;
using Godot.Collections;

[GlobalClass]
public partial class UnlockItemsResult : ResearchResult
{
	[Export] public Array<ItemData> UnlockedItems { get; private set; } = new();

	protected override void OnApply(
		GlobeTeamHolder teamHolder,
		ResearchProject completedProject)
	{
		teamHolder.AddUnlockedItems(UnlockedItems);
	}
}
