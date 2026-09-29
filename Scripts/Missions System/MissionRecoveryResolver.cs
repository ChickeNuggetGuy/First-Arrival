using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

public sealed class MissionRecoveryResult
{
	public Godot.Collections.Array<
		Godot.Collections.Dictionary<string, Variant>> RecoveredUnits { get; } = new();

	public Godot.Collections.Dictionary<int, int> RecoveredItems { get; } = new();
	public Godot.Collections.Dictionary<int, int> SoldItems { get; } = new();

	public long SaleProceeds => GetItemTotal(SoldItems, useSalePrice: true);

	public long GetRecoveredItemWeight() =>
		GetItemTotal(RecoveredItems, useSalePrice: false);

	public int GetOriginalItemCount(int itemId)
	{
		long recovered = RecoveredItems.TryGetValue(itemId, out int recoveredCount)
			? recoveredCount
			: 0;
		long sold = SoldItems.TryGetValue(itemId, out int soldCount)
			? soldCount
			: 0;
		return (int)Math.Min(int.MaxValue, recovered + sold);
	}

	public bool TrySellItem(int itemId, int count = 1) =>
		TryMoveItem(RecoveredItems, SoldItems, itemId, count);

	public bool TryRestoreSoldItem(int itemId, int count = 1) =>
		TryMoveItem(SoldItems, RecoveredItems, itemId, count);

	private static bool TryMoveItem(
		Godot.Collections.Dictionary<int, int> source,
		Godot.Collections.Dictionary<int, int> destination,
		int itemId,
		int count)
	{
		if (count <= 0 ||
		    !source.TryGetValue(itemId, out int availableCount) ||
		    availableCount <= 0)
			return false;

		int destinationCount = destination.TryGetValue(
			itemId,
			out int storedCount)
			? storedCount
			: 0;
		int movedCount = Math.Min(
			Math.Min(count, availableCount),
			int.MaxValue - destinationCount);
		if (movedCount <= 0) return false;

		int remainingCount = availableCount - movedCount;
		if (remainingCount == 0) source.Remove(itemId);
		else source[itemId] = remainingCount;
		destination[itemId] = destinationCount + movedCount;
		return true;
	}

	private static long GetItemTotal(
		Godot.Collections.Dictionary<int, int> items,
		bool useSalePrice)
	{
		long total = 0;
		foreach ((int itemId, int count) in items)
		{
			if (count <= 0) continue;
			ItemData itemData = InventoryManager.Instance?.GetItemData(itemId);
			if (itemData == null || itemData is Craft) continue;

			long value = useSalePrice
				? Math.Max(0, itemData.sellPrice)
				: Math.Max(0, itemData.weight);
			long itemTotal = value * count;
			total = total > long.MaxValue - itemTotal
				? long.MaxValue
				: total + itemTotal;
		}
		return total;
	}
}

public static class MissionRecoveryResolver
{
	public static MissionRecoveryResult Resolve(
		MissionBase mission,
		Enums.MissionStatus outcome,
		InventoryGrid deploymentInventory = null,
		InventoryGrid heldInventory = null)
	{
		var result = new MissionRecoveryResult();
		if (mission == null || mission.RecoveryType == Enums.MissionRecoveryType.None)
			return result;

		bool fullField = outcome == Enums.MissionStatus.Successful &&
		                 mission.RecoveryType ==
		                 Enums.MissionRecoveryType.FullFieldOnSuccess;
		bool startingCellsOnly = outcome == Enums.MissionStatus.Failed ||
		                         outcome == Enums.MissionStatus.Timeout ||
		                         outcome == Enums.MissionStatus.Aborted ||
		                         outcome == Enums.MissionStatus.Successful &&
		                         mission.RecoveryType ==
		                         Enums.MissionRecoveryType.StartingCellsOnly;

		if (!fullField && !startingCellsOnly)
			return result;

		HashSet<GridCell> startingCells = GetStartingCells();
		HashSet<GridObject> recoveredUnits = RecoverPlayerUnits(
			result,
			fullField,
			startingCells);

		if (outcome == Enums.MissionStatus.Aborted && recoveredUnits.Count == 0)
			return new MissionRecoveryResult();

		IEnumerable<GridCell> recoveredCells = fullField
			? GridSystem.Instance?.AllGridCells ?? System.Array.Empty<GridCell>()
			: startingCells;

		foreach (GridCell cell in recoveredCells)
			AddInventory(cell?.InventoryGrid, result.RecoveredItems);

		AddInventory(deploymentInventory, result.RecoveredItems);
		AddInventory(heldInventory, result.RecoveredItems);
		if (fullField)
			RecoverItemsFromUnits(
				result.RecoveredItems,
				recoveredUnits);

		return result;
	}

	private static HashSet<GridCell> GetStartingCells()
	{
		var cells = new HashSet<GridCell>();
		if (GridSystem.Instance?.AllGridCells == null) return cells;

		foreach (GridCell cell in GridSystem.Instance.AllGridCells)
		{
			if (cell != null && cell.UnitTeamSpawn == Enums.UnitTeam.Player)
				cells.Add(cell);
		}

		return cells;
	}

	private static HashSet<GridObject> RecoverPlayerUnits(
		MissionRecoveryResult result,
		bool fullField,
		HashSet<GridCell> startingCells)
	{
		var recovered = new HashSet<GridObject>();
		GridObjectTeamHolder playerHolder = GridObjectManager.Instance?
			.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (playerHolder?.GridObjects == null ||
		    !playerHolder.GridObjects.TryGetValue(
			    Enums.GridObjectState.Active,
			    out List<GridObject> activeUnits))
			return recovered;

		foreach (GridObject unit in playerHolder.GridObjects.Values.SelectMany(units => units).Distinct())
		{
			if (unit == null || !GodotObject.IsInstanceValid(unit) ||
			    !(unit.IsActive || unit.Condition?.State == Enums.UnitCondition.Unconscious) ||
			    unit.Condition?.State == Enums.UnitCondition.Dead ||
			    !fullField && !IsOnStartingCell(unit, startingCells))
				continue;

			Godot.Collections.Dictionary<string, Variant> savedUnit = unit.Save();
			MoveUnitInventoryToRecovery(unit, savedUnit, result.RecoveredItems);
			savedUnit["Team"] = (int)Enums.UnitTeam.Player;
			savedUnit["IsActive"] = false;
			savedUnit["HasPosition"] = false;
			// Stun is a battle condition; evacuation ends it, while health and fatal
			// wounds stay on the soldier's original component save data.
			if (unit.Condition != null)
			{
				var nodes = savedUnit["Nodes"].AsGodotDictionary<string, Variant>();
				var condition = nodes[unit.Condition.Name].AsGodotDictionary<string, Variant>();
				condition["state"] = (int)Enums.UnitCondition.Conscious;
				condition["stimulantRecovery"] = false;
				foreach (var node in nodes.Values)
				{
					var values = node.AsGodotDictionary<string, Variant>();
					if (values.TryGetValue("Stun", out var stun))
						stun.AsGodotDictionary<string, Variant>()["current"] = 0;
				}
				if (nodes.TryGetValue("Stun", out var stunNode))
					stunNode.AsGodotDictionary<string, Variant>()["current"] = 0;
			}
			result.RecoveredUnits.Add(savedUnit);
			recovered.Add(unit);
		}

		return recovered;
	}

	private static void MoveUnitInventoryToRecovery(
		GridObject unit,
		Godot.Collections.Dictionary<string, Variant> savedUnit,
		Godot.Collections.Dictionary<int, int> recoveredItems)
	{
		if (!unit.TryGetGridObjectNode<GridObjectInventory>(out var inventory) ||
		    !savedUnit.TryGetValue("Nodes", out Variant nodesValue) ||
		    nodesValue.VariantType != Variant.Type.Dictionary)
			return;

		foreach (InventoryGrid grid in inventory.InventoryGrids.Values)
			AddInventory(grid, recoveredItems);

		var inventoryTypes = new Godot.Collections.Array<int>();
		var inventories =
			new Godot.Collections.Dictionary<string, Variant>();
		foreach (Enums.InventoryType inventoryType in inventory.InventoryGrids.Keys)
		{
			inventoryTypes.Add((int)inventoryType);
			inventories[inventoryType.ToString()] =
				new Godot.Collections.Dictionary<string, Variant>
				{
					["items"] = new Godot.Collections.Array<
						Godot.Collections.Dictionary<string, Variant>>()
				};
		}

		var nodes = nodesValue.AsGodotDictionary<string, Variant>();
		nodes[inventory.Name.ToString()] =
			new Godot.Collections.Dictionary<string, Variant>
			{
				["inventory_types"] = inventoryTypes,
				["inventories"] = inventories
			};
		savedUnit["Nodes"] = nodes;
	}

	private static void RecoverItemsFromUnits(
		Godot.Collections.Dictionary<int, int> recoveredItems,
		HashSet<GridObject> recoveredUnits)
	{
		if (GridObjectManager.Instance == null) return;

		var visited = new HashSet<GridObject>();
		foreach (GridObjectTeamHolder holder in GridObjectManager.Instance
		         .GetGridObjectTeamHolders().Values)
		{
			if (holder?.GridObjects == null) continue;

			foreach (List<GridObject> units in holder.GridObjects.Values)
			{
				foreach (GridObject unit in units)
				{
					if (unit == null || !GodotObject.IsInstanceValid(unit) ||
					    recoveredUnits.Contains(unit) || !visited.Add(unit) ||
					    !unit.TryGetGridObjectNode<GridObjectInventory>(out var inventory))
						continue;

					foreach (InventoryGrid grid in inventory.InventoryGrids.Values)
						AddInventory(grid, recoveredItems);
				}
			}
		}
	}

	private static bool IsOnStartingCell(
		GridObject gridObject,
		HashSet<GridCell> startingCells)
	{
		GridCell bodyCell = gridObject?.Condition?.Body?.GetWorldCell();
		if (bodyCell != null) return startingCells.Contains(bodyCell);
		GridPositionData position = gridObject?.GridPositionData;
		if (position?.AnchorCell != null && startingCells.Contains(position.AnchorCell))
			return true;

		if (position?.OccupiedCells == null) return false;
		foreach (GridCell cell in position.OccupiedCells)
		{
			if (startingCells.Contains(cell)) return true;
		}

		return false;
	}

	private static void AddInventory(
		InventoryGrid inventory,
		Godot.Collections.Dictionary<int, int> recoveredItems)
	{
		if (inventory?.UniqueItems == null) return;

		foreach ((Item item, int count) in inventory.UniqueItems)
		{
			ItemData itemData = item?.ItemData;
			if (itemData == null || itemData is Craft ||
			    itemData.ItemID < 0 || count <= 0)
				continue;

			int currentCount = recoveredItems.TryGetValue(
				itemData.ItemID,
				out int storedCount)
				? storedCount
				: 0;
			recoveredItems[itemData.ItemID] = (int)Math.Min(
				int.MaxValue,
				(long)currentCount + count);
		}
	}
}
