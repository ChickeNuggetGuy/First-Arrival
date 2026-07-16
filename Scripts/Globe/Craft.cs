using System.Threading.Tasks;
using Godot;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class Craft : ItemData
{
	[Export]
	public int MaxSpeed { get; set; }

	[Export]
	public int Acceleration { get; set; }

	[Export]
	public bool IsAvailable { get; set; }

	[Export(PropertyHint.Range, "0,20,1")]
	public int DetectionRadius { get; set; } = 2;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float DetectionChance { get; set; } = 0.35f;

	[Export]
	public bool ShowDetectionRadius { get; set; } = true;

	[Export]
	public Enums.CraftStatus Status { get; set; } =
		Enums.CraftStatus.Home;

	[Export]
	public int CurrentCellIndex { get; set; } = -1;

	[Export]
	public int HomeBaseIndex { get; set; } = -1;

	protected TeamBaseCellDefinition baseCellDefinition;

	[Export]
	public int TargetCellIndex { get; set; } = -1;

	public MeshInstance3D visual { get; protected set; }
	private Enums.UnitTeam _revealedToTeams = Enums.UnitTeam.None;

	private Array<GridObject> stationedGridObjects = new();
	private Dictionary<int, int> itemCounts = new();

	public int Index { get; private set; }

	public bool IsVisibleTo(Enums.UnitTeam team)
		=> team != Enums.UnitTeam.None && (_revealedToTeams & team) != 0;

	public bool RevealForTeam(Enums.UnitTeam team)
	{
		if (team == Enums.UnitTeam.None || IsVisibleTo(team)) return false;
		_revealedToTeams |= team;
		if (visual != null && GodotObject.IsInstanceValid(visual))
			visual.Visible = true;
		return true;
	}

	public void Setup(
		int index,
		int homeBaseIndex,
		TeamBaseCellDefinition baseDefinition
	)
	{
		Index = index;

		if (HomeBaseIndex == -1)
		{
			HomeBaseIndex = homeBaseIndex;
		}

		if (CurrentCellIndex == -1)
		{
			CurrentCellIndex = homeBaseIndex;
		}

		baseCellDefinition = baseDefinition;
	}

	public void AddStationedUnit(GridObject unit)
	{
		if (unit == null || stationedGridObjects.Contains(unit))
		{
			return;
		}

		stationedGridObjects.Add(unit);
	}

	public void RemoveStationedUnit(GridObject unit)
	{
		if (unit == null)
		{
			return;
		}

		stationedGridObjects.Remove(unit);
	}

	public Array<GridObject> GetStationedUnits()
	{
		return stationedGridObjects;
	}

	public void GoToBase()
	{
		GD.Print("Sending craft home");
		TeamBaseCellDefinition baseDef = GetBaseCellDefinition();
		baseDef.SendCraft(
			CurrentCellIndex,
			baseDef.cellIndex,
			this,
			GlobeTeamManager.Instance
		);
	}

public bool TryAddStationedGridObject(GridObject gridObject)
{
	if (gridObject == null) return false;
	if (stationedGridObjects.Contains(gridObject)) return false;

	gridObject.SetIsActive(false);
	gridObject.Visible = false;
	stationedGridObjects.Add(gridObject);
	return true;
}

public bool TryRemoveStationedGridObject(GridObject gridObject)
{
	if (gridObject == null) return false;
	if (!stationedGridObjects.Contains(gridObject)) return false;

	stationedGridObjects.Remove(gridObject);
	return true;
}

public Godot.Collections.Array<GridObject> GetStationedGridObjects() =>
	stationedGridObjects;

public Dictionary<int, int> GetItemCounts => itemCounts;

public bool TryAddItem(int itemId, int count)
{
	ItemData itemData = InventoryManager.Instance?.GetItemData(itemId);
	if (itemData == null || itemData is Craft || count <= 0) return false;

	int currentCount = itemCounts.TryGetValue(itemId, out int storedCount)
		? storedCount
		: 0;
	itemCounts[itemId] = currentCount + count;
	return true;
}

public bool TryRemoveItem(int itemId, int count)
{
	if (count <= 0 || !itemCounts.TryGetValue(itemId, out int currentCount) ||
	    currentCount < count)
		return false;

	int remainingCount = currentCount - count;
	if (remainingCount == 0) itemCounts.Remove(itemId);
	else itemCounts[itemId] = remainingCount;
	return true;
}

public new Godot.Collections.Dictionary<string, Variant> Save()
{
	return new Godot.Collections.Dictionary<string, Variant>
	{
		{ "itemID", ItemID },
		{ "index", Index },
		{ "status", (int)Status },
		{ "currentCellIndex", CurrentCellIndex },
		{ "targetCellIndex", TargetCellIndex },
		{ "homeBaseIndex", HomeBaseIndex },
		{ "maxSpeed", MaxSpeed },
		{ "acceleration", Acceleration },
		{ "isAvailable", IsAvailable },
		{ "detectionRadius", DetectionRadius },
		{ "detectionChance", DetectionChance },
		{ "showDetectionRadius", ShowDetectionRadius },
		{ "revealedToTeams", (int)_revealedToTeams },
		{ "stationedItems", itemCounts },
		{
			"stationedUnits",
			GridObjectSerializationUtility.SaveGridObjects(
				stationedGridObjects
			)
		},
	};
}

private void LoadDataOnly(
	Godot.Collections.Dictionary<string, Variant> data
)
{
	itemCounts.Clear();
	if (data.ContainsKey("stationedItems"))
	{
		var savedItemCounts = data["stationedItems"].AsGodotDictionary();
		foreach (Variant key in savedItemCounts.Keys)
		{
			int itemId = key.AsInt32();
			int count = savedItemCounts[key].AsInt32();
			if (count > 0) itemCounts[itemId] = count;
		}
	}

	if (data.ContainsKey("index")) Index = data["index"].AsInt32();
	if (data.ContainsKey("status"))
	{
		Status = (Enums.CraftStatus)data["status"].AsInt32();
	}
	if (data.ContainsKey("currentCellIndex"))
	{
		CurrentCellIndex = data["currentCellIndex"].AsInt32();
	}
	if (data.ContainsKey("targetCellIndex"))
	{
		TargetCellIndex = data["targetCellIndex"].AsInt32();
	}
	if (data.ContainsKey("homeBaseIndex"))
	{
		HomeBaseIndex = data["homeBaseIndex"].AsInt32();
	}
	if (data.ContainsKey("maxSpeed"))
	{
		MaxSpeed = data["maxSpeed"].AsInt32();
	}
	if (data.ContainsKey("acceleration"))
	{
		Acceleration = data["acceleration"].AsInt32();
	}
	if (data.ContainsKey("isAvailable"))
	{
		IsAvailable = data["isAvailable"].AsBool();
	}
	if (data.ContainsKey("detectionRadius"))
		DetectionRadius = data["detectionRadius"].AsInt32();
	if (data.ContainsKey("detectionChance"))
		DetectionChance = data["detectionChance"].AsSingle();
	if (data.ContainsKey("showDetectionRadius"))
		ShowDetectionRadius = data["showDetectionRadius"].AsBool();
	if (data.ContainsKey("revealedToTeams"))
		_revealedToTeams = (Enums.UnitTeam)data["revealedToTeams"].AsInt32();
}

// Keep your existing Load(data) if other code calls it.
public void Load(Godot.Collections.Dictionary<string, Variant> data)
{
	LoadDataOnly(data);
}

public async Task LoadAsync(
	Godot.Collections.Dictionary<string, Variant> data,
	Node unitParent
)
{
	LoadDataOnly(data);

	stationedGridObjects.Clear();

	if (!data.ContainsKey("stationedUnits"))
	{
		return;
	}

	var savedUnits =
		data["stationedUnits"]
			.AsGodotArray<Godot.Collections.Dictionary<string, Variant>>();

	stationedGridObjects =
		await GridObjectSerializationUtility.LoadGridObjectsAsync(
			savedUnits,
			unitParent,
			true
		);
}

	public TeamBaseCellDefinition GetBaseCellDefinition()
	{
		return baseCellDefinition;
	}

	public void SetBaseCellDefinition(TeamBaseCellDefinition definition)
	{
		baseCellDefinition = definition;
	}

	public MeshInstance3D GetVisual()
	{
		return visual;
	}

	public void SetVisual(MeshInstance3D instance)
	{
		visual = instance;
	}
}
