using Godot;
using System;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GrantPointsResult : ResearchResult
{
	[Export(PropertyHint.Range, "0,100000,1,or_greater")]
	public int Amount { get; private set; }

	protected override void OnApply(
		GlobeTeamHolder teamHolder,
		ResearchProject completedProject)
	{
		if (Amount > 0)
			teamHolder.AddMonthlyScore(Amount, Enums.MonthlyScoreReason.Research);
	}
}
