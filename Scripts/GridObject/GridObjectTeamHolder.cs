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

    [Export] public Array<PackedScene> unitPrefabs;
    public int VisibilityMinX { get; private set; }
    public int VisibilityMinY { get; private set; }
    public int VisibilityMinZ { get; private set; }
    public int VisibilityWidth { get; private set; }
    public int VisibilityHeight { get; private set; } 
    public int VisibilityDepth { get; private set; } 
    
    public System.Collections.Generic.Dictionary<Enums.GridObjectState, List<GridObject>> GridObjects { get; protected set; }
    public GridObject CurrentGridObject { get; protected set; }

    // Stores 2D slices for debug
    [Export] public Godot.Collections.Dictionary<int, ImageTexture> VisibilityTextures = new();
    private readonly Godot.Collections.Dictionary<int, Image> _visibilityImages = new();
    private readonly Godot.Collections.Array<Image> _visibilitySlices = new();
    private readonly HashSet<int> _dirtyVisibilitySlices = new();
    private readonly List<GridCell> _newlyVisibleCells = new();
    private GridCell[][,] _visibilityGrid;
    private bool _textureNeedsCreate;
    private bool _textureUploadQueued;
    
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
        foreach (var body in GetChildren().OfType<FirstArrival.Scripts.Inventory_System.UnitBodyItem>())
            body.QueueFree();
        _visibilityGrid = null;
        TeamVisibleCells.Clear();
        TeamNoLongerVisibleCells.Clear();
        ExploredCells.Clear();
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

		    // The sight cache checks grid revision, position and heading, including
		    // changes made outside actions. Repeated completion callbacks can reuse it.
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

        _newlyVisibleCells.Clear();
        foreach (var cell in newTeamVisible)
            if (!TeamVisibleCells.Contains(cell)) _newlyVisibleCells.Add(cell);

	    TeamVisibleCells.Clear();
	    TeamVisibleCells.UnionWith(newTeamVisible);
	    ExploredCells.UnionWith(TeamVisibleCells);
        
        // Gameplay fog must be current before the next action or enemy check.
        // Only the GPU upload is deferred, combining instant steps in one frame.
        bool initialized = EnsureVisibilityImages();
        if (!initialized)
        {
            foreach (var cell in TeamNoLongerVisibleCells)
                SetCellVisibility(cell, false);
            foreach (var cell in _newlyVisibleCells)
                SetCellVisibility(cell, true);
        }

        if (initialized || _dirtyVisibilitySlices.Count > 0)
            QueueVisibilityTextureUpload();
    }

    private bool EnsureVisibilityImages()
    {
        var grid = GridSystem.Instance?.GridCells;
        if (grid == null || ReferenceEquals(_visibilityGrid, grid)) return false;
        var allCells = GridSystem.Instance.AllGridCells;
        if (allCells == null || allCells.Length == 0) return false;

        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        foreach (var cell in allCells)
        {
            if (cell == null) continue;
            var coords = cell.GridCoordinates;
            minX = System.Math.Min(minX, coords.X);
            minY = System.Math.Min(minY, coords.Y);
            minZ = System.Math.Min(minZ, coords.Z);
            maxX = System.Math.Max(maxX, coords.X);
            maxY = System.Math.Max(maxY, coords.Y);
            maxZ = System.Math.Max(maxZ, coords.Z);
        }
        if (minX == int.MaxValue) return false;

        _visibilityGrid = grid;
        VisibilityMinX = minX;
        VisibilityMinY = minY;
        VisibilityMinZ = minZ;
        VisibilityWidth = maxX - minX + 1;
        VisibilityHeight = maxZ - minZ + 1;
        VisibilityDepth = maxY - minY + 1;
        _visibilityImages.Clear();
        _visibilitySlices.Clear();
        VisibilityTextures.Clear();
        VisibilityTexturesForDebug.Clear();
        _dirtyVisibilitySlices.Clear();

        for (int y = 0; y < VisibilityDepth; y++)
        {
            var image = Image.Create(VisibilityWidth, VisibilityHeight, false, Image.Format.Rgba8);
            image.Fill(Colors.Black);
            _visibilityImages[y] = image;
            _visibilitySlices.Add(image);
            var texture = ImageTexture.CreateFromImage(image);
            VisibilityTextures[y] = texture;
            VisibilityTexturesForDebug.Add(texture);
            _dirtyVisibilitySlices.Add(y);
        }

        // Initialize all cells once per grid; subsequent updates touch only the
        // visible area and cells that have just left it, never the map volume.
        foreach (var cell in allCells)
        {
            if (cell == null) continue;
            bool visible = TeamVisibleCells.Contains(cell);
            bool explored = ExploredCells.Contains(cell);
            if (visible || explored)
                SetVisibilityPixel(cell, visible ? Colors.White : new Color(0.5f, 0.5f, 0.5f));
            SetGameplayFog(cell, visible, explored);
        }
        _textureNeedsCreate = true;
        return true;
    }

    private void SetCellVisibility(GridCell cell, bool visible)
    {
        SetVisibilityPixel(cell, visible ? Colors.White : new Color(0.5f, 0.5f, 0.5f));
        SetGameplayFog(cell, visible, true);
    }

    private void SetGameplayFog(GridCell cell, bool visible, bool explored)
    {
        if (Team != Enums.UnitTeam.Player) return;
        var state = visible ? Enums.FogState.Visible :
            explored ? Enums.FogState.PreviouslySeen : Enums.FogState.Unseen;
        if (cell.fogState != state) cell.SetFogState(state);
    }

    private void SetVisibilityPixel(GridCell cell, Color color)
    {
        var coords = cell.GridCoordinates;
        int y = coords.Y - VisibilityMinY;
        int x = coords.X - VisibilityMinX;
        int z = coords.Z - VisibilityMinZ;
        if (!_visibilityImages.TryGetValue(y, out var image) ||
            x < 0 || z < 0 || x >= VisibilityWidth || z >= VisibilityHeight) return;
        if (image.GetPixel(x, z) == color) return;
        image.SetPixel(x, z, color);
        _dirtyVisibilitySlices.Add(y);
    }

    private void QueueVisibilityTextureUpload()
    {
        if (_textureUploadQueued) return;
        _textureUploadQueued = true;
        Callable.From(FlushVisibilityTexture).CallDeferred();
    }

    private void FlushVisibilityTexture()
    {
        _textureUploadQueued = false;
        if (_dirtyVisibilitySlices.Count == 0) return;
        foreach (int y in _dirtyVisibilitySlices)
            VisibilityTextures[y].Update(_visibilityImages[y]);

        if (_textureNeedsCreate)
        {
            VisibilityTexture3D.Create(Image.Format.Rgba8, VisibilityWidth,
                VisibilityHeight, VisibilityDepth, false, _visibilitySlices);
            _textureNeedsCreate = false;
        }
        else
        {
            VisibilityTexture3D.Update(_visibilitySlices);
        }
        _dirtyVisibilitySlices.Clear();
        EmitSignal(SignalName.VisibilityChanged, (int)Team, VisibilityTexture3D);
    }

    public List<GridCell> GetVisibleGridCells() => TeamVisibleCells.ToList();

    public void UpdateGridObjects(ActionDefinition actionCompleted, ActionDefinition currentAction)
    {
        GridObject sightSource = actionCompleted?.parentGridObject ?? CurrentGridObject;
        if (sightSource != null && sightSource.IsInitialized)
        {
            if (sightSource.TryGetGridObjectNode<GridObjectSight>(out var sight))
            {
                // A manual refresh may follow an external physics change. Action
                // refreshes use the revision-aware cache checked for every viewer.
                if (actionCompleted == null) sight.MarkDirty();
            }
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
        if (gridObject != null && !gridObject.CanAct) return;
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
        if (gridObject.Condition != null)
        {
            gridObject.Condition.Evaluate();
            return;
        }
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

    public async void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        await LoadAsync(data);
    }

    public async Task LoadAsync(Godot.Collections.Dictionary<string, Variant> data)
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
                    await newUnit.LoadAsync(unitData);
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
                    await newUnit.LoadAsync(unitData);
                    GridObjects[Enums.GridObjectState.Inactive].Add(newUnit);
                    newUnit.SetIsActive(false); 
                    newUnit.Hide();
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
