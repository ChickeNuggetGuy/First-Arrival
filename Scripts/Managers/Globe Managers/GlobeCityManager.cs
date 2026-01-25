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

    public override string GetManagerName() => "GlobeCityManager";

    protected override async Task _Setup(bool loadingData) => await Task.CompletedTask;

    protected override async Task _Execute(bool loadingData)
    {
	    if (loadingData && HasLoadedData && citiesData != null)
	    {
		    foreach (var kvp in citiesData)
		    {
			    int cellIndex = kvp.Key;
			    var cityData = kvp.Value;
			    string cityName = cityData.ContainsKey("city") ? cityData["city"].AsString() : "City";

			    var cell = GlobeHexGridManager.Instance.GetCellFromIndex(cellIndex);
			    if (cell.HasValue)
			    {
				    SpawnCity(cell.Value, cityName);
			    }
		    }
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
                SpawnCity(cell.Value, cityName);
                if (!citiesData.ContainsKey(cell.Value.Index))
					citiesData.Add(cell.Value.Index, cityData);
            }
        }

        GD.Print($"City Data Loaded: {citiesData.Count}");
        

        EmitSignal(SignalName.ExecuteCompleted);
        await Task.CompletedTask;
    }

    private void SpawnCity(HexCellData cell, string name)
    {
        if (_cityPrefab == null) return;

        Node3D cityInstance = _cityPrefab.Instantiate<Node3D>();
        if (_cityContainer != null) _cityContainer.AddChild(cityInstance);
        else AddChild(cityInstance);

        cityInstance.GlobalPosition = cell.Center;

        // Orient to face outward from sphere center
        Vector3 surfaceNormal = cell.Center.Normalized();
        Vector3 upDir = Mathf.Abs(surfaceNormal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
        cityInstance.LookAt(cell.Center + surfaceNormal, upDir);
        
        cityInstance.Name = name;
    }

    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
	    Godot.Collections.Dictionary<string,Variant> data = new Godot.Collections.Dictionary<string,Variant>();
	    
	    data.Add("cityData", citiesData);
	    
	    return data;
    }

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
	    base.Load(data);
	    if (!HasLoadedData) return;

	    if (data.ContainsKey("cityData"))
	    {
		    citiesData = data["cityData"].AsGodotDictionary<int, Dictionary>();
	    }
    }


    public override void _Input(InputEvent @event)
    {
	    base._Input(@event);
	    
	    if (InputManager.Instance == null) return;
	    if (InputManager.Instance.CurrentCell == null) return;
	    
	    if (@event is InputEventMouseButton eventButton && eventButton.Pressed)
	    {
		    if (eventButton.ButtonIndex == MouseButton.Left)
		    {
			    int cellIndex = InputManager.Instance.CurrentCell.Value.Index;
			    
			    if (!citiesData.ContainsKey(cellIndex)) return;

			    GD.Print(citiesData[cellIndex]["city"].AsString());
		    }
	    }
    }
    
    public override void Deinitialize()
    {
	    return;
    }
}