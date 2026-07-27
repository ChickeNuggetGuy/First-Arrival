using Godot;
using System;
using Godot.Collections;

/// <summary>
/// Gameplay representation of a city placed on a globe hex. The visual is
/// deliberately owned through <see cref="HexCellDefinition.BindVisual"/>, so
/// city logic can continue to address the city even when its scene node is
/// recreated after loading a save.
/// </summary>
public partial class CityCellDefinition : HexCellDefinition
{
	/// <summary>Source record used to create this city (country, population, and ID).</summary>
	public Dictionary CityData { get; }

	public string Country => CityData.ContainsKey("country")
		? CityData["country"].AsString()
		: string.Empty;

	public double Population => CityData.ContainsKey("population")
		? CityData["population"].AsDouble()
		: 0.0;

	public CityCellDefinition(int cellIndex, string name, bool startsHidden = false)
		: this(cellIndex, name, new Dictionary(), startsHidden)
	{
	}

	public CityCellDefinition(
		int cellIndex,
		string name,
		Dictionary cityData,
		bool startsHidden = false) : base(cellIndex, name, startsHidden)
	{
		CityData = cityData ?? new Dictionary();
	}
}
