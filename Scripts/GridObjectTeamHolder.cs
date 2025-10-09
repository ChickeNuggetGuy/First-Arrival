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

    public System.Collections.Generic.Dictionary<Enums.GridObjectState, List<GridObject>> GridObjects { get; protected set; }

    public GridObject CurrentGridObject { get; protected set; }

    [Export] public Godot.Collections.Dictionary<int, ImageTexture> VisibilityTextures =
	    new Godot.Collections.Dictionary<int, ImageTexture>();
    [Export] public Godot.Collections.Array<ImageTexture> VisibilityTexturesForDebug { get; private set; } = new Godot.Collections.Array<ImageTexture>();


    #region Signals
    [Signal]
    public delegate void SelectedGridObjectChangedEventHandler(GridObject gridObject);

    [Signal]
    public delegate void GridObjectListChangedEventHandler(
	    GridObjectTeamHolder gridObjectTeamHolder
    );
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

        Vector3I mapSize = MeshTerrainGenerator.Instance.GetMapCellSize();
        for (int y = 0; y < mapSize.Y; y++)
        {
	        VisibilityTextures[y] =  ImageTexture.CreateFromImage(Image.CreateEmpty(mapSize.X,mapSize.Z, false, Image.Format.Rgba8));
	        VisibilityTextures[y].GetImage().Fill(Colors.Black);
        }
    }

    public HashSet<GridCell> ExploredCells { get; private set; } = new();

    public void UpdateVisibility()
    {
        var currentlyVisibleCells = new HashSet<GridCell>();
        var gridObjects = GridObjects[Enums.GridObjectState.Active];

        foreach (var gridObject in gridObjects)
        {
            var sight = gridObject.gridObjectNodesDictionary["all"]
                .FirstOrDefault(n => n is GridObjectSight) as GridObjectSight;

            if (sight != null)
            {
	            sight.CalculateSightArea();
	            IReadOnlyList<GridCell> visibleCells = sight.VisibleCells;
	            for (var i = 0; i < visibleCells.Count; i++)
	            {
		            var cell = sight.VisibleCells[i];
		            currentlyVisibleCells.Add(cell);
	            }
            }
        }
        foreach (var cell in currentlyVisibleCells)
        {
            ExploredCells.Add(cell);
        }

        // 3. Determine the state of each (x, z) column based on the new visibility rules.
        var visibleColumns = new HashSet<Vector2I>();
        foreach (var cell in currentlyVisibleCells)
        {
            visibleColumns.Add(new Vector2I(cell.gridCoordinates.X, cell.gridCoordinates.Z));
        }

        var exploredColumns = new HashSet<Vector2I>();
        foreach (var cell in ExploredCells)
        {
            exploredColumns.Add(new Vector2I(cell.gridCoordinates.X, cell.gridCoordinates.Z));
        }

        // 4. Update the state of all cells and visibility textures.
        var allCells = GridSystem.Instance.AllGridCells;
        if (allCells == null || !allCells.Any()) return;

        // Update Textures
        int minX = allCells.Min(c => c.gridCoordinates.X);
        int maxX = allCells.Max(c => c.gridCoordinates.X);
        int minZ = allCells.Min(c => c.gridCoordinates.Z);
        int maxZ = allCells.Max(c => c.gridCoordinates.Z);

        int gridWidth = maxX - minX + 1;
        int gridHeight = maxZ - minZ + 1;

        var yLevels = allCells.Select(c => c.gridCoordinates.Y).Distinct().ToList();
        yLevels.Sort();

        if (VisibilityTexturesForDebug == null)
        {
            VisibilityTexturesForDebug = new Godot.Collections.Array<ImageTexture>();
        }
        VisibilityTexturesForDebug.Clear();

        foreach (var y in yLevels)
        {
            Image image;
            if (VisibilityTextures.TryGetValue(y, out var texture))
            {
                image = texture.GetImage();
                if (image.GetWidth() != gridWidth || image.GetHeight() != gridHeight)
                {
                    image.Resize(gridWidth, gridHeight);
                }
            }
            else
            {
                image = Image.Create(gridWidth, gridHeight, false, Image.Format.Rgba8);
                texture = ImageTexture.CreateFromImage(image);
                VisibilityTextures[y] = texture;
            }

            image.Fill(Colors.Black);
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    var columnPos = new Vector2I(x + minX, z + minZ);
                    Color pixelColor;

                    if (visibleColumns.Contains(columnPos))
                    {
                        pixelColor = Colors.White;
                    }
                    else if (exploredColumns.Contains(columnPos))
                    {
                        pixelColor = new Color(1, 1, 1, 0.5f);
                    }
                    else
                    {
                        pixelColor = Colors.Black;
                    }

                    if(pixelColor != Colors.Black)
                        image.SetPixel(x, z, pixelColor);
                }
            }
            texture.Update(image);
            VisibilityTexturesForDebug.Add(texture);
        }

        foreach (var cell in allCells)
        {
            var columnPos = new Vector2I(cell.gridCoordinates.X, cell.gridCoordinates.Z);
            Enums.FogState newState;

            if (visibleColumns.Contains(columnPos))
            {
                newState = Enums.FogState.Visible;
            }
            else if (exploredColumns.Contains(columnPos))
            {
                newState = Enums.FogState.PreviouslySeen;
            }
            else
            {
                newState = Enums.FogState.Unseen;
            }
            cell.SetFogState(newState);
        }
    }

    public void UpdateGridObjects(
        ActionDefinition actionCompleted,
        ActionDefinition currentAction
    )
    {
        GD.Print("Updating grid objects");

        foreach (GridObject gridObject in GridObjects[Enums.GridObjectState.Active])
        {
            GridObjectSight sightArea =
                gridObject.gridObjectNodesDictionary["all"]
                    .FirstOrDefault(node => node is GridObjectSight) as GridObjectSight;

            if (sightArea != null)
            {
                sightArea.CalculateSightArea();
            }
        }

        UpdateVisibility();
        
    }

    public GridObject GetNextGridObject()
    {
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

        if (gridObject.TryGetStat(Enums.Stat.Health, out GridObjectStat health))
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
}