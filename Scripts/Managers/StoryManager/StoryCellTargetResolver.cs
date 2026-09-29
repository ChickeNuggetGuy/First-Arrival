using System.Collections.Generic;
using FirstArrival.Scripts.Globe.Countries;

public enum StoryCellTargetMode
{
	CustomHexCellIndex,
	RandomHexCell,
	RandomCityCell
}

/// <summary>
/// Shared target resolution for story events that operate on globe cells.
/// Country filtering uses the globe's country atlas, so cities and ordinary
/// cells follow the same country boundaries.
/// </summary>
public static class StoryCellTargetResolver
{
	public static List<int> GetCandidates(
		StoryCellTargetMode targetMode,
		int customHexCellIndex,
		bool limitToCountry,
		CountryDefinition targetCountry)
	{
		var candidates = new List<int>();
		GlobeHexGridManager gridManager = GlobeHexGridManager.Instance;
		if (gridManager == null || (limitToCountry && targetCountry == null))
			return candidates;

		uint countryKey = limitToCountry ? targetCountry.CountryKey : 0;
		switch (targetMode)
		{
			case StoryCellTargetMode.CustomHexCellIndex:
			{
				// Retain the old MissionStoryEvent convention: a negative custom
				// index means a random land cell.
				if (customHexCellIndex < 0)
					goto case StoryCellTargetMode.RandomHexCell;

				HexCellData? cell = gridManager.GetCellFromIndex(
					customHexCellIndex,
					excludeWater: true);
				if (cell.HasValue &&
				    (!limitToCountry ||
				     gridManager.GetCountryKeyForIndex(customHexCellIndex) == countryKey))
				{
					candidates.Add(customHexCellIndex);
				}
				break;
			}

			case StoryCellTargetMode.RandomHexCell:
				candidates.AddRange(
					gridManager.GetCellIndicesSnapshot(
						excludeWater: true,
						countryKey));
				break;

			case StoryCellTargetMode.RandomCityCell:
				GlobeCityManager cityManager = GlobeCityManager.Instance;
				if (cityManager != null)
				{
					candidates.AddRange(
						cityManager.GetCityCellIndicesForCountry(countryKey));
				}
				break;
		}

		return candidates;
	}
}
