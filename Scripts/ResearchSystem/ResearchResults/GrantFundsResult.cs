using Godot;

[GlobalClass]
public partial class GrantFundsResult : ResearchResult
{
	[Export(PropertyHint.Range, "0,1000000000000,1,or_greater")]
	public long Amount { get; private set; }
	[Export] public string FinanceCategory { get; private set; } = "Research rewards";

	protected override void OnApply(
		GlobeTeamHolder teamHolder,
		ResearchProject completedProject)
	{
		if (Amount > 0)
			teamHolder.ChangeFunds(Amount, FinanceCategory);
	}
}
