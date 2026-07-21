using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FirstArrival.Scripts.Globe.Countries;

/// <summary>
/// Inspector-editable source data for all colors in the country atlas.
/// Create or select this resource, assign the atlas, then toggle Sync From Country Map.
/// </summary>
[Tool]
[GlobalClass]
public partial class CountryDatabase : Resource
{
	[ExportGroup("Source")]
	[Export] public Texture2D CountryMap { get; set; }

	[ExportGroup("Countries")]
	[Export] public Array<CountryDefinition> Countries { get; set; } = new();

	[ExportGroup("Editor Tools")]
	[Export]
	private bool SyncFromCountryMap
	{
		get => false;
		set
		{
			if (value)
				ScanCountryMap();
		}
	}

	[Export(PropertyHint.MultilineText)]
	public string LastSyncResult { get; private set; } =
		"Assign the country texture, then enable Sync From Country Map.";

	private readonly System.Collections.Generic.Dictionary<uint, CountryDefinition> _lookup = new();

	public CountryDefinition GetCountry(uint countryKey)
	{
		EnsureLookup();
		return _lookup.GetValueOrDefault(countryKey);
	}

	public bool TryGetCountry(uint countryKey, out CountryDefinition definition)
	{
		EnsureLookup();
		return _lookup.TryGetValue(countryKey, out definition);
	}

	/// <summary>
	/// Returns a detached set that is safe to read from the globe's parallel grid build.
	/// These are the canonical colors; antialiased atlas pixels are not identities.
	/// </summary>
	public HashSet<uint> GetCountryKeysSnapshot()
	{
		EnsureLookup();
		return new HashSet<uint>(_lookup.Keys);
	}

	public void RebuildLookup()
	{
		_lookup.Clear();
		if (Countries == null)
			return;

		foreach (CountryDefinition country in Countries)
		{
			if (country == null || country.CountryKey == 0)
				continue;

			if (!_lookup.TryAdd(country.CountryKey, country))
				GD.PushWarning(
					$"CountryDatabase: duplicate map color on '{country.CountryName}'."
				);
		}
	}

	private void EnsureLookup()
	{
		if (_lookup.Count == 0 && Countries is { Count: > 0 })
			RebuildLookup();
	}

	private void ScanCountryMap()
	{
		if (CountryMap == null)
		{
			LastSyncResult = "No country texture is assigned.";
			NotifyPropertyListChanged();
			return;
		}

		Image image = CountryMap.GetImage();
		if (image == null)
		{
			LastSyncResult = "The assigned texture could not be read on the CPU.";
			NotifyPropertyListChanged();
			return;
		}

		if (image.IsCompressed() && image.Decompress() != Error.Ok)
		{
			LastSyncResult = "The assigned texture could not be decompressed.";
			NotifyPropertyListChanged();
			return;
		}

		if (image.GetFormat() != Image.Format.Rgba8)
			image.Convert(Image.Format.Rgba8);

		byte[] bytes = image.GetData().ToArray();
		var pixelCounts = new System.Collections.Generic.Dictionary<uint, int>();
		var colorsWithSolidInterior = new HashSet<uint>();
		int width = image.GetWidth();
		int height = image.GetHeight();

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				uint key = ReadCountryKey(bytes, width, x, y);
				if (key == 0)
					continue;

				pixelCounts[key] = pixelCounts.GetValueOrDefault(key) + 1;

				// Real country fills have a solid interior. Antialias colors form
				// narrow bands and fail this 3x3 test even when their
				// total pixel count is high along long borders.
				if (!colorsWithSolidInterior.Contains(key)
				    && x >= 1 && y >= 1 && x < width - 1 && y < height - 1
				    && ReadCountryKey(bytes, width, x - 1, y) == key
				    && ReadCountryKey(bytes, width, x + 1, y) == key
				    && ReadCountryKey(bytes, width, x, y - 1) == key
				    && ReadCountryKey(bytes, width, x, y + 1) == key
				    && ReadCountryKey(bytes, width, x - 1, y - 1) == key
				    && ReadCountryKey(bytes, width, x + 1, y - 1) == key
				    && ReadCountryKey(bytes, width, x - 1, y + 1) == key
				    && ReadCountryKey(bytes, width, x + 1, y + 1) == key)
				{
					colorsWithSolidInterior.Add(key);
				}
			}
		}

		var existing = new System.Collections.Generic.Dictionary<uint, CountryDefinition>();
		if (Countries != null)
		{
			foreach (CountryDefinition country in Countries)
			{
				if (country != null && country.CountryKey != 0)
				existing.TryAdd(country.CountryKey, country);
			}
		}

		int added = 0;
		int preserved = 0;
		var synced = new Array<CountryDefinition>();
		foreach (uint key in pixelCounts
			.Where(pair => colorsWithSolidInterior.Contains(pair.Key))
			.Select(pair => pair.Key)
			.OrderBy(key => key))
		{
			if (existing.TryGetValue(key, out CountryDefinition country))
			{
				preserved++;
			}
			else
			{
				country = new CountryDefinition
				{
					MapColor = CountryDefinition.CountryKeyToColor(key),
					CountryName = $"Unnamed #{key & 0xff:X2}{(key >> 8) & 0xff:X2}{(key >> 16) & 0xff:X2}"
				};
				added++;
			}

			country.ResourceName = country.CountryName;
			synced.Add(country);
		}

		Countries = synced;
		RebuildLookup();
		LastSyncResult =
			$"Found {synced.Count} countries. Added {added}; preserved {preserved}. " +
			"Thin antialiased border colors were ignored.";
		EmitChanged();
		NotifyPropertyListChanged();
		GD.Print($"CountryDatabase: {LastSyncResult}");

#if TOOLS
		if (!string.IsNullOrEmpty(ResourcePath))
		{
			Error saveError = ResourceSaver.Save(this);
			if (saveError != Error.Ok)
				GD.PushError($"CountryDatabase could not save '{ResourcePath}': {saveError}");
		}
#endif
	}

	private static uint ReadCountryKey(byte[] rgba, int width, int x, int y)
	{
		int index = ((y * width) + x) * 4;
		byte r = rgba[index];
		byte g = rgba[index + 1];
		byte b = rgba[index + 2];
		byte a = rgba[index + 3];

		if (a <= 26 || (r == 0 && g == 0 && b == 0))
			return 0;

		return (uint)(r | (g << 8) | (b << 16));
	}
}
