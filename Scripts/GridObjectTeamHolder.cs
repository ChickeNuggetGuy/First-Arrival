using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GridObjectTeamHolder : Node
{
    [Export] public Enums.UnitTeam Team { get; private set; }
    [Export] public Enums.UnitTeam EnemyTeams { get; private set; }
    [Export] private Node _activeUnitsHolder;
    [Export] private Node _inactiveUnitsHolder;

    
    public int VisibilityMinX { get; private set; }
    public int VisibilityMinZ { get; private set; }
    public int VisibilityWidth { get; private set; }
    public int VisibilityHeight { get; private set; }
    public System.Collections.Generic.Dictionary<Enums.GridObjectState, List<GridObject>> GridObjects { get; protected set; }

    public GridObject CurrentGridObject { get; protected set; }

    [Export] public Godot.Collections.Dictionary<int, ImageTexture> VisibilityTextures =
	    new Godot.Collections.Dictionary<int, ImageTexture>();
    private readonly Godot.Collections.Dictionary<int, Image> _visibilityImages =
	    new Godot.Collections.Dictionary<int, Image>();
    [Export] public Godot.Collections.Array<ImageTexture> VisibilityTexturesForDebug { get; private set; } = new Godot.Collections.Array<ImageTexture>();


    #region Signals
    [Signal]
    public delegate void SelectedGridObjectChangedEventHandler(GridObject gridObject);

    [Signal]
    public delegate void GridObjectListChangedEventHandler(
	    GridObjectTeamHolder gridObjectTeamHolder
    );
    
    [Signal]
    public delegate void VisibilityChangedEventHandler(GridObjectTeamHolder gridObjectTeamHolder);
    #endregion


    public void Setup()
    {
	    GridObjects =
		    new System.Collections.Generic.Dictionary<Enums.GridObjectState, List<GridObject>>()
		    {
			    { Enums.GridObjectState.Active, new List<GridObject>() },
			    { Enums.GridObjectState.Inactive, new List<GridObject>() },
		    };

	    if (_activeUnitsHolder == null)
	    {
		    _activeUnitsHolder = new Node { Name = "ActiveUnits" };
		    AddChild(_activeUnitsHolder);
	    }

	    if (_inactiveUnitsHolder == null)
	    {
		    _inactiveUnitsHolder = new Node { Name = "InactiveUnits" };
		    AddChild(_inactiveUnitsHolder);
	    }
		
	    // Ensure any pre-existing children in the scene tree are cleaned up or registered
	    // For a clean setup, we usually want to clear them unless we are in the editor
	    foreach(Node child in _activeUnitsHolder.GetChildren()) child.QueueFree();
	    foreach(Node child in _inactiveUnitsHolder.GetChildren()) child.QueueFree();

	    Vector3I mapSize = MeshTerrainGenerator.Instance.GetMapCellSize();
	    for (int y = 0; y < mapSize.Y; y++)
	    {
		    var image = Image.CreateEmpty(mapSize.X, mapSize.Z, false, Image.Format.Rgba8);
		    image.Fill(Colors.Black);
		    _visibilityImages[y] = image;
		    VisibilityTextures[y] = ImageTexture.CreateFromImage(image);
	    }
    }

    public HashSet<GridCell> ExploredCells { get; private set; } = new();

   public void UpdateVisibility()
{
    var currentlyVisibleCells = new HashSet<GridCell>();
    var gridObjects = GridObjects[Enums.GridObjectState.Active];

    foreach (var gridObject in gridObjects)
    {
	    if (!gridObject.TryGetGridObjectNode<GridObjectSight>(out var sight)) continue;

        if (sight != null)
        {
            sight.CalculateSightArea();
            foreach (var cell in sight.VisibleCells)
                currentlyVisibleCells.Add(cell);
        }
    }

    foreach (var cell in currentlyVisibleCells)
        ExploredCells.Add(cell);

    var allCells = GridSystem.Instance.AllGridCells;
    if (allCells == null || !allCells.Any()) return;

    int minX = allCells.Min(c => c.gridCoordinates.X);
    int maxX = allCells.Max(c => c.gridCoordinates.X);
    int minZ = allCells.Min(c => c.gridCoordinates.Z);
    int maxZ = allCells.Max(c => c.gridCoordinates.Z);

    VisibilityMinX = minX;
    VisibilityMinZ = minZ;
    VisibilityWidth = MeshTerrainGenerator.Instance.GetMapCellSize().X;
    VisibilityHeight = MeshTerrainGenerator.Instance.GetMapCellSize().Y;
    
    int gridWidth = maxX - minX + 1;
    int gridHeight = maxZ - minZ + 1;

    var yLevels = allCells.Select(c => c.gridCoordinates.Y)
                          .Distinct()
                          .OrderBy(v => v);

    VisibilityTexturesForDebug ??= new Godot.Collections.Array<ImageTexture>();
    VisibilityTexturesForDebug.Clear();

    foreach (var y in yLevels)
    {
        Image image;
        if (!_visibilityImages.TryGetValue(y, out image) ||
            image.GetWidth() != gridWidth || image.GetHeight() != gridHeight)
        {
            // Create a new Image and a new ImageTexture when size changes
            image = Image.Create(gridWidth, gridHeight, false, Image.Format.Rgba8);
            _visibilityImages[y] = image;
            VisibilityTextures[y] = ImageTexture.CreateFromImage(image);
        }

        image.Fill(Colors.Black);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                var gridCoords = new Vector3I(x + minX, y, z + minZ);
                GridCell cell = GridSystem.Instance.GetGridCell(gridCoords);

                Color pixelColor = Colors.Black;
                if (cell != GridCell.Null)
                {
                    if (currentlyVisibleCells.Contains(cell))
                        pixelColor = Colors.White;
                    else if (ExploredCells.Contains(cell))
                        pixelColor = Colors.DarkGray;
                }

                if (pixelColor != Colors.Black)
                    image.SetPixel(x, z, pixelColor);
            }
        }

        var tex = VisibilityTextures[y];
        tex.Update(image);

        VisibilityTexturesForDebug.Add(tex);
    }

    foreach (var cell in allCells)
    {
        Enums.FogState newState =
            currentlyVisibleCells.Contains(cell) ? Enums.FogState.Visible :
            ExploredCells.Contains(cell) ? Enums.FogState.PreviouslySeen :
            Enums.FogState.Unseen;

        cell.SetFogState(newState);
    }

    EmitSignal(SignalName.VisibilityChanged, this);
}

    private static int _updateCounter = 0;

    public List<GridCell> GetVisibleGridCells()
    {
        var visibleCells = new List<GridCell>();
        var allCells = GridSystem.Instance.AllGridCells;

        if (allCells == null || !allCells.Any())
        {
            return visibleCells;
        }

        int minX = allCells.Min(c => c.gridCoordinates.X);
        int minZ = allCells.Min(c => c.gridCoordinates.Z);

        foreach (var entry in _visibilityImages)
        {
            int y = entry.Key;
            Image image = entry.Value;

            for (int x = 0; x < image.GetWidth(); x++)
            {
                for (int z = 0; z < image.GetHeight(); z++)
                {
                    if (image.GetPixel(x, z).IsEqualApprox(Colors.White))
                    {
                        var gridCoords = new Vector3I(x + minX, y, z + minZ);
                        GridCell cell = GridSystem.Instance.GetGridCell(gridCoords);
                        if (cell != GridCell.Null)
                        {
                            visibleCells.Add(cell);
                        }
                    }
                }
            }
        }
	GD.Print("GetVisibleGridCells: " + visibleCells.Count);
        return visibleCells;
    }

    public void UpdateGridObjects(
        ActionDefinition actionCompleted,
        ActionDefinition currentAction
    )
    {
        GD.Print("Updating grid objects");
        UpdateVisibility();
    }

    public GridObject GetNextGridObject()
    {
	    if (GridObjects[Enums.GridObjectState.Active].Count == 0) return null;
		
	    if (CurrentGridObject == null) CurrentGridObject = GridObjects[Enums.GridObjectState.Active][0];

	    if (CurrentGridObject == null) return null;
	    int index =
		    GridObjects[Enums.GridObjectState.Active].IndexOf(CurrentGridObject);

	    if (index == -1)
		    return null;

	    int nextIndex =
		    ((index + 1) >= GridObjects[Enums.GridObjectState.Active].Count)
			    ? 0
			    : index + 1;

	    SetSelectedGridObject(
		    GridObjects[Enums.GridObjectState.Active][nextIndex]
	    );
	    return CurrentGridObject;
    }

    public void SetSelectedGridObject(GridObject gridObject)
    {
        GridObject oldGridObject = CurrentGridObject;
        CurrentGridObject = gridObject;
        GD.Print("1");
        EmitSignal(SignalName.SelectedGridObjectChanged, CurrentGridObject);
    }

    public async Task AddGridObject(GridObject gridObject)
    {
        while (!gridObject.IsInitialized)
        {
            await Task.Yield();
        }
        GridObjects[Enums.GridObjectState.Active].Add(gridObject);

        gridObject.GetParent()?.RemoveChild(gridObject);
        _activeUnitsHolder.AddChild(gridObject); // Reparent to active holder

        if(!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return;
        if (statHolder.TryGetStat(Enums.Stat.Health, out GridObjectStat health))
        {
            health.CurrentValueMin += HealthOnCurrentValueMin;
        }

        EmitSignal(SignalName.GridObjectListChanged, this);
    }

    private void HealthOnCurrentValueMin(int value, GridObject gridObject)
    {
        if (gridObject == CurrentGridObject)
        {
            // Select a new currentObject
            GetNextGridObject();
        }
        GridObjects[Enums.GridObjectState.Active].Remove(gridObject);
        GridObjects[Enums.GridObjectState.Inactive].Add(gridObject);

        gridObject.GetParent()?.RemoveChild(gridObject); // Remove from current parent
        _inactiveUnitsHolder.AddChild(gridObject); // Add to inactive holder

        gridObject.SetIsActive(false);
        gridObject.Hide();
        gridObject.Position = new(-100, -100, -100);
        EmitSignal(SignalName.GridObjectListChanged, this);
    }

    public bool IsGridObjectActive(GridObject gridObject)
    {
        return GridObjects[Enums.GridObjectState.Active].Contains(gridObject);
    }


public Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>();
		
		// 1. Save Active Units
		var activeList = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
		foreach (var obj in GridObjects[Enums.GridObjectState.Active])
		{
			// ASSUMPTION: GridObject has a Save() method returning Dictionary<string, Variant>
			// AND it includes "Filename" : obj.SceneFilePath
			activeList.Add(obj.Save());
		}
		data["Active"] = activeList;

		// 2. Save Inactive Units
		var inactiveList = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
		foreach (var obj in GridObjects[Enums.GridObjectState.Inactive])
		{
			inactiveList.Add(obj.Save());
		}
		data["Inactive"] = inactiveList;
		
		// 3. Save Team Info
		data["Team"] = (int)Team;

		return data;
	}

	public void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		Setup(); // Ensure holders and dicts are initialized

		// 1. Clear existing units (destroying them to avoid duplicates)
		foreach (var unit in GridObjects[Enums.GridObjectState.Active]) unit.QueueFree();
		foreach (var unit in GridObjects[Enums.GridObjectState.Inactive]) unit.QueueFree();
		
		GridObjects[Enums.GridObjectState.Active].Clear();
		GridObjects[Enums.GridObjectState.Inactive].Clear();

		// 2. Load Active Units
		if (data.ContainsKey("Active"))
		{
			var activeList = (Godot.Collections.Array)data["Active"];
			foreach (Godot.Collections.Dictionary<string, Variant> unitData in activeList)
			{
				GridObject newUnit = InstantiateUnitFromData(unitData);
				if (newUnit != null)
				{
					_activeUnitsHolder.AddChild(newUnit);
					GridObjects[Enums.GridObjectState.Active].Add(newUnit);
					
					// Re-hook events
					if (newUnit.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder))
					{
						if (statHolder.TryGetStat(Enums.Stat.Health, out GridObjectStat health))
						{
							health.CurrentValueMin += HealthOnCurrentValueMin;
						}
					}
				}
			}
		}

		// 3. Load Inactive Units
		if (data.ContainsKey("Inactive"))
		{
			var inactiveList = (Godot.Collections.Array)data["Inactive"];
			foreach (Godot.Collections.Dictionary<string, Variant> unitData in inactiveList)
			{
				GridObject newUnit = InstantiateUnitFromData(unitData);
				if (newUnit != null)
				{
					_inactiveUnitsHolder.AddChild(newUnit);
					GridObjects[Enums.GridObjectState.Inactive].Add(newUnit);
					newUnit.SetIsActive(false); // Helper to set internal state
				}
			}
		}
		
		EmitSignal(SignalName.GridObjectListChanged, this);
	}

	private GridObject InstantiateUnitFromData(Godot.Collections.Dictionary<string, Variant> unitData)
	{
		// Requires "Filename" to be saved in the GridObject.Save() method
		if (!unitData.ContainsKey("Filename"))
		{
			GD.PrintErr("Save data missing Filename for GridObject");
			return null;
		}

		string scenePath = unitData["Filename"].AsString();
		var scene = GD.Load<PackedScene>(scenePath);
		if (scene == null)
		{
			GD.PrintErr($"Failed to load scene at path: {scenePath}");
			return null;
		}

		GridObject unit = scene.Instantiate<GridObject>();
		
		// Load internal data (Position, Stats, etc.)
		unit.Load(unitData); 
		
		return unit;
	}
}
