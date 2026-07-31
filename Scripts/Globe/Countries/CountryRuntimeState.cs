namespace FirstArrival.Scripts.Globe.Countries;

/// <summary>Mutable, save-game-specific values for a country.</summary>
public sealed class CountryRuntimeState
{
	public uint CountryKey { get; }
	public CountryDefinition Definition { get; }
	public double GrossDomesticProduct { get; set; }
	public float PlayerOpinion { get; set; }
	// Kept for compatibility with existing save files. Country funding now comes
	// from the balanced global pool owned by GlobeTeamManager.
	public double MonthlyContributionRate { get; set; }

	public string CountryName => Definition?.CountryName ?? $"Unknown country {CountryKey}";

	public CountryRuntimeState(uint countryKey, CountryDefinition definition)
	{
		CountryKey = countryKey;
		Definition = definition;
		GrossDomesticProduct = definition?.GrossDomesticProduct ?? 0.0;
		PlayerOpinion = definition?.PlayerOpinion ?? 0.0f;
		MonthlyContributionRate = System.Math.Clamp(
			(definition?.InitialMonthlyContributionPercent ?? 0.01) / 100.0,
			0.0,
			1.0);
	}

	public long GetMonthlyContribution()
	{
		double contribution = System.Math.Floor(
			System.Math.Max(0.0, GrossDomesticProduct) *
			System.Math.Clamp(MonthlyContributionRate, 0.0, 1.0));
		if (double.IsNaN(contribution) || contribution <= 0.0) return 0;
		return contribution >= long.MaxValue ? long.MaxValue : (long)contribution;
	}

	public double GetFundingWeight(double gdpExponent)
	{
		double gdp = System.Math.Max(0.0, GrossDomesticProduct);
		if (gdp <= 0.0 || double.IsNaN(gdp) || double.IsInfinity(gdp))
			return 0.0;

		return System.Math.Pow(
			gdp,
			System.Math.Clamp(gdpExponent, 0.1, 1.0));
	}

	public void ChangePlayerOpinion(float amount)
	{
		PlayerOpinion = Godot.Mathf.Clamp(PlayerOpinion + amount, -100.0f, 100.0f);
	}
}
