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

    [Export] public PackedScene unitPrefab;
    public int VisibilityMinX { get; private set; }
    public int VisibilityMinZ { get; private set; }
    public int VisibilityWidth { get; private set; }
    public int VisibilityHeight { get; private set; } 
    public int VisibilityDepth { get; private set; } 
    
    public System.Collections.Generic.Dictionary<Enums.GridObjectState, List<GridObject>> GridObjects { get; protected set; }
    public GridObject CurrentGridObject { get; protected set; }

    // Stores 2D slices for debug
    [Export] public Godot.Collections.Dictionary<int, ImageTexture> VisibilityTextures = new();
    private readonly Godot.Collections.Dictionary<int, Image> _visibilityImages = new();
    
    //used by visibility Shader
    public ImageTexture3D VisibilityTexture3D { get; private set; } = new ImageTexture3D();

    [Export] public Godot.Collections.Array<ImageTexture> VisibilityTexturesForDebug { get; private set; } = new();
	
    public HashSet<GridCell> TeamVisibleCells { get; private set; } = new();
    public HashSet<GridCell> ExploredCells { get; private set; } = new();
    public HashSet<GridCell> TeamNoLongerVisibleCells { get; private set; } = new();

    #region Signals
    [Signal] public delegate void SelectedGridObjectChangedEventHandler(GridObject gridObject);
    [Signal] public delegate void GridObjectListChangedEventHandler(GridObjectTeamHolder gridObjectTeamHolder);
    // Updated signal to pass the 3D texture directly
    [Signal] public delegate void VisibilityChangedEventHandler(Enums.UnitTeam team, ImageTexture3D texture);
    #endregion

    public void Setup()
    {
        GridObjects = new System.Collections.Generic.Dictionary<Enums.GridObjectState, List<GridObject>>()
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
        
        foreach(Node child in _activeUnitsHolder.GetChildren()) child.QueueFree();
        foreach(Node child in _inactiveUnitsHolder.GetChildren()) child.QueueFree();

        if(MeshTerrainGenerator.Instance != null)
        {
            Vector3I mapSize = MeshTerrainGenerator.Instance.GetMapCellSize();
            VisibilityDepth = mapSize.Y; 
        }
    }

    /// <summary>
    /// Recalculates team visibility union, updates tracking sets, and refreshes the texture.
    /// </summary>
    public void UpdateVisibility()
    {
	    var gridObjects = GridObjects[Enums.GridObjectState.Active];
	    var newTeamVisible = new HashSet<GridCell>();

	    foreach (var gridObject in gridObjects)
	    {
		    if (gridObject == null || !gridObject.IsInitialized) continue;

		    if (!gridObject.TryGetGridObjectNode<GridObjectSight>(out var sight) || sight == null) continue;

		    sight.EnsureUpToDate();

		    foreach (var cell in sight.VisibleCells)
		    {
			    if (cell != null && cell != GridCell.Null)
				    newTeamVisible.Add(cell);
		    }
	    }

	    TeamNoLongerVisibleCells.Clear();
	    TeamNoLongerVisibleCells.UnionWith(TeamVisibleCells);
	    TeamNoLongerVisibleCells.ExceptWith(newTeamVisible);

	    TeamVisibleCells.Clear();
	    TeamVisibleCells.UnionWith(newTeamVisible);
	    ExploredCells.UnionWith(TeamVisibleCells);
        
        // GD.Print($"UpdateVisibility: Team {Team}, ActiveUnits: {gridObjects.Count}, VisibleCells: {TeamVisibleCells.Count}");

	    UpdateVisibilityTextures();

        // Pass Team and Texture to the manager
	    EmitSignal(SignalName.VisibilityChanged, (int)Team, VisibilityTexture3D);
    }

    /// <summary>
    /// Generates Image slices and constructs the ImageTexture3D.
    /// </summary>
    private void UpdateVisibilityTextures()
    {
        var allCells = GridSystem.Instance.AllGridCells;
        if (allCells == null || !allCells.Any())
        {
            GD.PrintErr("UpdateVisibilityTextures: No cells found!");
            return;
        }

        int minX = allCells.Min(c => c.GridCoordinates.X);
        int maxX = allCells.Max(c => c.GridCoordinates.X);
        int minZ = allCells.Min(c => c.GridCoordinates.Z);
        int maxZ = allCells.Max(c => c.GridCoordinates.Z);
        
        // Calculate Y bounds dynamically to ensure we cover all grid layers
        int minY = allCells.Min(c => c.GridCoordinates.Y);
        int maxY = allCells.Max(c => c.GridCoordinates.Y);

        VisibilityMinX = minX;
        VisibilityMinZ = minZ;
        VisibilityWidth = maxX - minX + 1;
        VisibilityHeight = maxZ - minZ + 1; 
        VisibilityDepth = maxY - minY + 1;
        
        //  data for 3D Texture
        Godot.Collections.Array<Image> allSlices = new Godot.Collections.Array<Image>();

        VisibilityTexturesForDebug ??= new Godot.Collections.Array<ImageTexture>();
        VisibilityTexturesForDebug.Clear();

        // Iterate through all Y levels defined by the map
        for (int i = 0; i < VisibilityDepth; i++)
        {
            int y = minY + i;
            Image image;
            
            // Check cache (using relative index i for cache key to keep 0-based index for texture array)
            if (!_visibilityImages.TryGetValue(i, out image) ||
                image.GetWidth() != VisibilityWidth || image.GetHeight() != VisibilityHeight)
            {
                image = Image.Create(VisibilityWidth, VisibilityHeight, false, Image.Format.Rgba8);
                _visibilityImages[i] = image;
                VisibilityTextures[i] = ImageTexture.CreateFromImage(image);
            }

            // Fill black 
            image.Fill(Colors.Black);

            // Paint pixels on this slice
            for (int x = 0; x < VisibilityWidth; x++)
            {
                for (int z = 0; z < VisibilityHeight; z++)
                {
                    var gridCoords = new Vector3I(x + minX, y, z + minZ);
                    GridCell cell = GridSystem.Instance.GetGridCell(gridCoords);

                    Color pixelColor = Colors.Black;
                    
                    if (cell != GridCell.Null)
                    {
                        bool isVisible = TeamVisibleCells.Contains(cell);
                        bool isExplored = ExploredCells.Contains(cell);

                        if (isVisible)
                            pixelColor = Colors.White;
                        else if (isExplored)
                            pixelColor = new Color(0.5f, 0.5f, 0.5f);

                        // Logic to update Cell state for gameplay logic
                        if (Team == Enums.UnitTeam.Player)
                        {
                            Enums.FogState newState = isVisible ? Enums.FogState.Visible :
                                                      isExplored ? Enums.FogState.PreviouslySeen :
                                                      Enums.FogState.Unseen;
                            
                            if(cell.fogState != newState)
                            {
                                cell.SetFogState(newState);
                            }
                        }
                    }

                    if (pixelColor != Colors.Black)
                        image.SetPixel(x, z, pixelColor);
                }
            }

            // Update debug texture
            var tex = VisibilityTextures[i];
            tex.Update(image);
            VisibilityTexturesForDebug.Add(tex);
            
            // Add to stack for 3D Texture
            allSlices.Add(image);
        }

        //Create or Update ImageTexture3D
        VisibilityTexture3D.Create(Image.Format.Rgba8, VisibilityWidth, VisibilityHeight, VisibilityDepth, false, allSlices);
    }

    public List<GridCell> GetVisibleGridCells() => TeamVisibleCells.ToList();

    public void UpdateGridObjects(ActionDefinition actionCompleted, ActionDefinition currentAction)
    {
	    GD.Print("UpdateGridObjects");
        if (CurrentGridObject != null && CurrentGridObject.IsInitialized)
        {
            if (CurrentGridObject.TryGetGridObjectNode<GridObjectSight>(out var sight))
                sight.CalculateSightArea();
        }
        UpdateVisibility();
    }

    public GridObject GetNextGridObject()
    {
        if (GridObjects[Enums.GridObjectState.Active].Count == 0) return null;
        if (CurrentGridObject == null) CurrentGridObject = GridObjects[Enums.GridObjectState.Active][0];

        int index = GridObjects[Enums.GridObjectState.Active].IndexOf(CurrentGridObject);
        if (index == -1) index = 0; 
        int nextIndex = (index + 1) >= GridObjects[Enums.GridObjectState.Active].Count ? 0 : index + 1;

        SetSelectedGridObject(GridObjects[Enums.GridObjectState.Active][nextIndex]);
        return CurrentGridObject;
    }

    public void SetSelectedGridObject(GridObject gridObject)
    {
        CurrentGridObject = gridObject;
        EmitSignal(SignalName.SelectedGridObjectChanged, CurrentGridObject);
    }

    public async Task AddGridObject(GridObject gridObject)
    {
        while (!gridObject.IsInitialized) await Task.Yield();
        
        if (!GridObjects[Enums.GridObjectState.Active].Contains(gridObject))
            GridObjects[Enums.GridObjectState.Active].Add(gridObject);

        if (gridObject.GetParent() != _activeUnitsHolder)
        {
            gridObject.GetParent()?.RemoveChild(gridObject);
            _activeUnitsHolder.AddChild(gridObject);
        }
        
        if(gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder))
        {
            if (statHolder.TryGetStat(Enums.Stat.Health, out GridObjectStat health))
            {
                health.CurrentValueMin -= HealthOnCurrentValueMin;
                health.CurrentValueMin += HealthOnCurrentValueMin;
            }
        }

        if (gridObject.TryGetGridObjectNode<GridObjectSight>(out var sight))
            sight.CalculateSightArea();

        UpdateVisibility();
        EmitSignal(SignalName.GridObjectListChanged, this);
    }

    private void HealthOnCurrentValueMin(int value, GridObject gridObject)
    {
        if (gridObject == CurrentGridObject) GetNextGridObject();
        
        GridObjects[Enums.GridObjectState.Active].Remove(gridObject);
        if (!GridObjects[Enums.GridObjectState.Inactive].Contains(gridObject))
            GridObjects[Enums.GridObjectState.Inactive].Add(gridObject);

        gridObject.Reparent(_inactiveUnitsHolder);
        gridObject.SetIsActive(false);
        gridObject.Hide();
        gridObject.Position = new(-100, -100, -100);
        
        UpdateVisibility(); 
        EmitSignal(SignalName.GridObjectListChanged, this);
    }

    public bool IsGridObjectActive(GridObject gridObject) => GridObjects[Enums.GridObjectState.Active].Contains(gridObject);
	
    public Godot.Collections.Dictionary<string, Variant> Save()
    {
        var data = new Godot.Collections.Dictionary<string, Variant>();
        
        var activeList = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
        foreach (var obj in GridObjects[Enums.GridObjectState.Active]) activeList.Add(obj.Save());
        data["Active"] = activeList;

        var inactiveList = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
        foreach (var obj in GridObjects[Enums.GridObjectState.Inactive]) inactiveList.Add(obj.Save());
        data["Inactive"] = inactiveList;
        
        data["Team"] = (int)Team;
        return data;
    }

    public void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        Setup();
        foreach (var unit in GridObjects[Enums.GridObjectState.Active]) unit.QueueFree();
        foreach (var unit in GridObjects[Enums.GridObjectState.Inactive]) unit.QueueFree();
        GridObjects[Enums.GridObjectState.Active].Clear();
        GridObjects[Enums.GridObjectState.Inactive].Clear();

        if (data.ContainsKey("Active"))
        {
            var activeList = (Godot.Collections.Array)data["Active"];
            foreach (Godot.Collections.Dictionary<string, Variant> unitData in activeList)
            {
                GridObject newUnit = InstantiateUnitFromData(unitData);
                if (newUnit != null)
                {
                    _activeUnitsHolder.AddChild(newUnit);
                    newUnit.Load(unitData); 
                    GridObjects[Enums.GridObjectState.Active].Add(newUnit);
                    
                    if (newUnit.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder))
                    {
                        if (statHolder.TryGetStat(Enums.Stat.Health, out GridObjectStat health))
                            health.CurrentValueMin += HealthOnCurrentValueMin;
                    }
                    if (newUnit.TryGetGridObjectNode<GridObjectSight>(out var sight))
                        sight.CalculateSightArea();
                }
            }
        }

        if (data.ContainsKey("Inactive"))
        {
            var inactiveList = (Godot.Collections.Array)data["Inactive"];
            foreach (Godot.Collections.Dictionary<string, Variant> unitData in inactiveList)
            {
                GridObject newUnit = InstantiateUnitFromData(unitData);
                if (newUnit != null)
                {
                    _inactiveUnitsHolder.AddChild(newUnit);
                    newUnit.Load(unitData); 
                    GridObjects[Enums.GridObjectState.Inactive].Add(newUnit);
                    newUnit.SetIsActive(false); 
                }
            }
        }
        UpdateVisibility();
        EmitSignal(SignalName.GridObjectListChanged, this);
    }

    private GridObject InstantiateUnitFromData(Godot.Collections.Dictionary<string, Variant> unitData)
    {
        if (!unitData.ContainsKey("Filename")) return null;
        string scenePath = unitData["Filename"].AsString();
        var scene = GD.Load<PackedScene>(scenePath);
        return scene?.Instantiate<GridObject>();
    }
}