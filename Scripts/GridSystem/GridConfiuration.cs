using FirstArrival.Scripts.Managers;
using Godot;

[GlobalClass, Tool]
public partial class GridConfiguration : Resource
{
    private static GridConfiguration _editorInstance;
    
    [Export] public Vector3 CellSize { get; set; } = new Vector3(1, 1, 1);
    [Export] public Vector3 GridWorldOrigin { get; set; } =  new Vector3(0,0.5f,0);
    [Export] public Vector3I GridSize { get; set; } = new Vector3I(20, 5, 20);
    
    /// <summary>
    /// Gets configuration from GridSystem at runtime, or loads editor config in editor.
    /// </summary>
    public static GridConfiguration GetActive()
    {
        // Runtime: use GridSystem values
        if (!Engine.IsEditorHint() && GridSystem.Instance != null)
        {
            return new GridConfiguration
            {
                CellSize = new Vector3(
                    GridSystem.Instance.CellSize.X,
                    GridSystem.Instance.CellSize.Y,
                    GridSystem.Instance.CellSize.X
                ),
                GridWorldOrigin = GridSystem.Instance.GridWorldOrigin,
                GridSize = GridSystem.Instance.GridSize
            };
        }
        
        // Editor: load from project settings or default resource
        _editorInstance ??= LoadEditorConfig();
        return _editorInstance;
    }
    
    private static GridConfiguration LoadEditorConfig()
    {
	    const string configPath = "res://Resources/GridConfiguration.tres";
    
	    if (ResourceLoader.Exists(configPath))
	    {
		    // Use 'as' to safely attempt cast. If it fails, it returns null instead of throwing exception.
		    var resource = GD.Load(configPath) as GridConfiguration;
        
		    if (resource != null)
		    {
			    return resource;
		    }
		    else
		    {
			    GD.PrintErr($"GridConfiguration exists at {configPath} but could not be cast to GridConfiguration type. Using default values.");
		    }
	    }
    
	    // Return sensible defaults if file doesn't exist OR if cast failed
	    return new GridConfiguration();
    }
    public Vector3I WorldToGrid(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - GridWorldOrigin;
        return new Vector3I(
            Mathf.FloorToInt(local.X / CellSize.X),
            Mathf.FloorToInt(local.Y / CellSize.Y),
            Mathf.FloorToInt(local.Z / CellSize.Z)
        );
    }
    
    public Vector3 GridToWorld(Vector3I gridCoords, bool cellCenter = true)
    {
        float offset = cellCenter ? 0.5f : 0f;
        return GridWorldOrigin + new Vector3(
            (gridCoords.X + offset) * CellSize.X,
            (gridCoords.Y + offset) * CellSize.Y,
            (gridCoords.Z + offset) * CellSize.Z
        );
    }
    
    public bool IsValidCoordinate(Vector3I coords)
    {
        return coords.X >= 0 && coords.X < GridSize.X &&
               coords.Y >= 0 && coords.Y < GridSize.Y &&
               coords.Z >= 0 && coords.Z < GridSize.Z;
    }
}