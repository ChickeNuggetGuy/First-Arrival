using System;
using System.Linq;
using Godot;

namespace FirstArrival.Scripts.Utility;

/// <summary>Creates unit names from the first- and last-name lists in Data/names.json.</summary>
public static class UnitNameGenerator
{
	private const string NamesPath = "res://Data/names.json";
	private static readonly Random Random = new();
	private static string[] _firstNames = Array.Empty<string>();
	private static string[] _lastNames = Array.Empty<string>();
	private static bool _loaded;

	public static string Generate()
	{
		LoadNames();

		if (_firstNames.Length == 0 && _lastNames.Length == 0) return "Unnamed Unit";
		if (_firstNames.Length == 0) return _lastNames[Random.Next(_lastNames.Length)];
		if (_lastNames.Length == 0) return _firstNames[Random.Next(_firstNames.Length)];

		return $"{_firstNames[Random.Next(_firstNames.Length)]} {_lastNames[Random.Next(_lastNames.Length)]}";
	}

	private static void LoadNames()
	{
		if (_loaded) return;
		_loaded = true;

		if (!FileAccess.FileExists(NamesPath))
		{
			GD.PrintErr($"Unit name data was not found at {NamesPath}.");
			return;
		}

		using var file = FileAccess.Open(NamesPath, FileAccess.ModeFlags.Read);
		var json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok)
		{
			GD.PrintErr($"Could not parse unit name data at {NamesPath}.");
			return;
		}

		var names = json.Data.AsGodotDictionary();
		_firstNames = ReadNames(names, "firstNames");
		_lastNames = ReadNames(names, "lastNames");
	}

	private static string[] ReadNames(Godot.Collections.Dictionary names, string key)
	{
		if (!names.TryGetValue(key, out Variant values)) return Array.Empty<string>();

		return values.AsGodotArray()
			.Select(value => value.AsString().Trim())
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.ToArray();
	}
}
