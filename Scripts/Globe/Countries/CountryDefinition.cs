using Godot;

namespace FirstArrival.Scripts.Globe.Countries;

/// <summary>
/// Designer-authored defaults for one country in the RGB country atlas.
/// Runtime changes are kept in CountryRuntimeState instead of modifying this resource.
/// </summary>
[Tool]
[GlobalClass]
public partial class CountryDefinition : Resource
{
	private string _countryName = "Unnamed Country";

	[ExportGroup("Identity")]
	[Export] public Color MapColor { get; set; } = Colors.White;
	[Export]
	public string CountryName
	{
		get => _countryName;
		set
		{
			_countryName = value;
			ResourceName = value;
		}
	}
	[Export] public string CountryCode { get; set; } = "";

	[ExportGroup("Starting Statistics")]
	[Export(PropertyHint.Range, "0,100000000000000,1000000,or_greater")]
	public double GrossDomesticProduct { get; set; }

	[Export(PropertyHint.Range, "-100,100,0.1")]
	public float PlayerOpinion { get; set; }

	/// <summary>The atlas RGB packed in exactly the same format used by GlobeHexGridManager.</summary>
	public uint CountryKey => ColorToCountryKey(MapColor);

	public static uint ColorToCountryKey(Color color)
	{
		byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(color.R * 255.0f), 0, 255);
		byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(color.G * 255.0f), 0, 255);
		byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(color.B * 255.0f), 0, 255);
		return (uint)(r | (g << 8) | (b << 16));
	}

	public static Color CountryKeyToColor(uint key)
	{
		return Color.Color8(
			(byte)(key & 0xff),
			(byte)((key >> 8) & 0xff),
			(byte)((key >> 16) & 0xff),
			255
		);
	}
}
