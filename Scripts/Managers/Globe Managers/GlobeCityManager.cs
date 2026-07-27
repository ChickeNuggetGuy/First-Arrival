using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot.Collections;

public partial class GlobeCityManager : Manager<GlobeCityManager>
{
    [Export] private PackedScene _cityPrefab;
    [Export] private string _dataPath = "res://top_10_cities_per_country.json";
    [Export] private Node3D _cityContainer;

    [ExportGroup("Alignment Settings")]
    [Export] private bool _flipLongitude = false; 
    [Export] private bool _flipLatitude = false;
    
    private Dictionary<int, Dictionary> citiesData = null;
	private readonly System.Collections.Generic.Dictionary<int, CityCellDefinition>
		_cityDefinitions = new();
	private int[] _cityCellIndices = System.Array.Empty<int>();

    public override string GetManagerName() => "GlobeCityManager";

    protected override async Task _Setup(bool loadingData) => await Task.CompletedTask;

    protected override async Task _Execute(bool loadingData)
    {
	    if (loadingData && HasLoadedData && citiesData != null)
	    {
		    RebuildCityDefinitions();
		    EmitSignal(SignalName.ExecuteCompleted);
		    return;
	    }

        if (!FileAccess.FileExists(_dataPath)) return;
		
        GlobeHexGridManager hexGridManager = GlobeHexGridManager.Instance;
        
        using var file = FileAccess.Open(_dataPath, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok) return;
        
        citiesData = new Dictionary<int, Dictionary>();

        var cityList = json.Data.AsGodotArray<Godot.Collections.Dictionary>();
		
        foreach (var cityData in cityList)
        {
            float lat = (float)cityData["lat"].AsDouble();
            float lng = (float)cityData["lng"].AsDouble();
            string cityName = cityData["city"].AsString();

            // 1. Apply Orientation Flips
            if (_flipLatitude) lat *= -1;
            if (_flipLongitude) lng *= -1;

            // 2. Apply Manual Offsets (to line up with your specific texture)
            float finalLat = lat;
            float finalLon = lng;

            // 3. Keep within standard bounds (-180 to 180, -90 to 90)
            finalLon = Mathf.PosMod(finalLon + 180, 360) - 180;
            finalLat = Mathf.Clamp(finalLat, -90, 90);

            Vector2 adjustedCoords = new Vector2(finalLat, finalLon);
            
            // Get the cell from the grid manager
            var cell = GlobeHexGridManager.Instance.GetCellFromLatLon(adjustedCoords);
			
            
            if (cell.HasValue)
            {
                if (!citiesData.ContainsKey(cell.Value.Index))
					citiesData.Add(cell.Value.Index, cityData);
            }
        }

        GD.Print($"City Data Loaded: {citiesData.Count}");
		RebuildCityDefinitions();

        EmitSignal(SignalName.ExecuteCompleted);
        await Task.CompletedTask;
    }

    private void RebuildCityDefinitions()
    {
        ClearCityDefinitions();
		RebuildCityCellIndex();
		if (citiesData == null || GlobeHexGridManager.Instance == null) return;

		foreach (var (cellIndex, cityData) in citiesData)
		{
			HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromIndex(cellIndex);
			if (!cell.HasValue) continue;

			string cityName = cityData.ContainsKey("city")
				? cityData["city"].AsString()
				: "City";
			var definition = new CityCellDefinition(cellIndex, cityName, cityData);
			_cityDefinitions.Add(cellIndex, definition);
			SpawnCity(cell.Value, definition);
		}
    }

    private void SpawnCity(HexCellData cell, CityCellDefinition definition)
    {
        if (_cityPrefab == null || definition == null) return;

		CellDefinitionVisual cityInstance = _cityPrefab.Instantiate<CellDefinitionVisual>();
        if (_cityContainer != null) _cityContainer.AddChild(cityInstance);
        else AddChild(cityInstance);

        cityInstance.GlobalPosition = cell.Center;
		definition.BindVisual(cityInstance);

        // Orient to face outward from sphere center
        Vector3 surfaceNormal = cell.Center.Normalized();
        Vector3 upDir = Mathf.Abs(surfaceNormal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
        cityInstance.LookAt(cell.Center + surfaceNormal, upDir);
        
        cityInstance.Name = definition.definitionName;
    }

    public override Dictionary<string, Variant> Save()
    {
	    Dictionary<string,Variant> data = new Dictionary<string,Variant>();
	    
	    data.Add("cityData", citiesData);
	    
	    return data;
    }

    public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
    {
	    if (!HasLoadedData) return Task.CompletedTask;

	    if (data.ContainsKey("cityData"))
	    {
		    citiesData = data["cityData"].AsGodotDictionary<int, Dictionary>();
		    RebuildCityCellIndex();
	    }
	    return Task.CompletedTask;
    }

	/// <summary>
	/// Returns a stable snapshot of city cell indices for strategic systems.
	/// The cache avoids scanning or allocating from the city dictionary every
	/// time the AI evaluates potential targets.
	/// </summary>
	public int[] GetCityCellIndices() => _cityCellIndices;

	/// <summary>Gets the persistent city definition assigned to a hex.</summary>
	public bool TryGetCityDefinition(int cellIndex, out CityCellDefinition definition)
		=> _cityDefinitions.TryGetValue(cellIndex, out definition);

	public string GetCityName(int cellIndex)
	{
		if (_cityDefinitions.TryGetValue(cellIndex, out CityCellDefinition definition))
			return definition.definitionName;

		if (citiesData != null && citiesData.TryGetValue(cellIndex, out Dictionary city) &&
		    city.ContainsKey("city"))
			return city["city"].AsString();

		return "City";
	}

	private void RebuildCityCellIndex()
	{
		if (citiesData == null || citiesData.Count == 0)
		{
			_cityCellIndices = System.Array.Empty<int>();
			return;
		}

		_cityCellIndices = new int[citiesData.Count];
		int index = 0;
		foreach (int cellIndex in citiesData.Keys)
			_cityCellIndices[index++] = cellIndex;
	}

	private void ClearCityDefinitions()
	{
		foreach (CityCellDefinition definition in _cityDefinitions.Values)
		{
			CellDefinitionVisual visual = definition.Visual;
			if (visual != null && GodotObject.IsInstanceValid(visual))
				visual.QueueFree();
			definition.ClearVisual();
		}

		_cityDefinitions.Clear();
	}


    public override void _Input(InputEvent @event)
    {
	    base._Input(@event);
	    
	    if (GlobeInputManager.Instance == null) return;
	    if (GlobeInputManager.Instance.CurrentCell == null) return;
	    
	    if (@event is InputEventMouseButton eventButton && eventButton.Pressed)
	    {
		    if (eventButton.ButtonIndex == MouseButton.Left)
		    {
			    int cellIndex = GlobeInputManager.Instance.CurrentCell.Value.Index;
			    
			    if (!_cityDefinitions.TryGetValue(cellIndex, out CityCellDefinition city)) return;

			    GD.Print(city.definitionName);
		    }
	    }
    }
    
    public override void Deinitialize()
    {
	    ClearCityDefinitions();
    }
}
