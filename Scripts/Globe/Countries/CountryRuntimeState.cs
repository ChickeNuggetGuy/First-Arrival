namespace FirstArrival.Scripts.Globe.Countries;

/// <summary>Mutable, save-game-specific values for a country.</summary>
public sealed class CountryRuntimeState
{
	public uint CountryKey { get; }
	public CountryDefinition Definition { get; }
	public double GrossDomesticProduct { get; set; }
	public float PlayerOpinion { get; set; }

	public string CountryName => Definition?.CountryName ?? $"Unknown country {CountryKey}";

	public CountryRuntimeState(uint countryKey, CountryDefinition definition)
	{
		CountryKey = countryKey;
		Definition = definition;
		GrossDomesticProduct = definition?.GrossDomesticProduct ?? 0.0;
		PlayerOpinion = definition?.PlayerOpinion ?? 0.0f;
	}

	public void ChangePlayerOpinion(float amount)
	{
		PlayerOpinion = Godot.Mathf.Clamp(PlayerOpinion + amount, -100.0f, 100.0f);
	}
}
