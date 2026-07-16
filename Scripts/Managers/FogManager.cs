using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class FogManager : Manager<FogManager>
{
    [Export] public ShaderMaterial FowMaterial { get; set; }
	[Export] public Godot.Collections.Array<ShaderMaterial> AdditionalFogMaterials { get; set; } = new();
	
    private ImageTexture3D _visibilityTexture3DCache;
    private GridObjectTeamHolder _playerHolder;

    public override string GetManagerName() => "FogManager";

    protected override Task _Setup(bool loadingData)
    {
        return Task.CompletedTask;
    }

    protected override Task _Execute(bool loadingData)
    {
        GD.Print("Executing Fog Manager...");
        InitFogGlobals();
        return Task.CompletedTask;
    }

    private void InitFogGlobals()
    {
        if (MeshTerrainGenerator.Instance == null)
        {
            GD.PrintErr("FogManager: MeshTerrainGenerator Instance is null!");
            return;
        }

        if (GridObjectManager.Instance == null)
        {
            GD.PrintErr("FogManager: GridObjectManager Instance is null!");
            return;
        }
        
        _playerHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
        if (_playerHolder == null)
        {
            GD.PrintErr("FogManager: Player GridObjectTeamHolder not found!");
            return;
        }
        
        _playerHolder.VisibilityChanged -= OnVisibilityChanged;
        _playerHolder.VisibilityChanged += OnVisibilityChanged;

        var playerTexture = _playerHolder.VisibilityTexture3D;

        if (playerTexture == null)
        {
            GD.Print("FogManager: Player visibility texture is not yet initialized.");
            return;
        }
		
        SetVisibilityTexture(playerTexture);
    }

    /// <summary>
    /// Event handler for when any team updates their visibility.
    /// </summary>
    private void OnVisibilityChanged(Enums.UnitTeam team, ImageTexture3D texture)
    {
        if (team == Enums.UnitTeam.Player)
        {
            SetVisibilityTexture(texture);
        }
    }

    private void SetVisibilityTexture(ImageTexture3D texture)
    {
        _visibilityTexture3DCache = texture;
        
        if (_visibilityTexture3DCache == null || FowMaterial == null || _playerHolder == null)
            return;

        Vector2 cellSize = GridSystem.Instance?.CellSize ?? MeshTerrainGenerator.Instance.cellSize;
        Vector3 cellSizeWorld = new Vector3(cellSize.X, cellSize.Y, cellSize.X);
        Vector3 gridOrigin = GridSystem.Instance?.GridWorldOrigin ?? Vector3.Zero;
        Vector3 visibilityMin = new Vector3(
            _playerHolder.VisibilityMinX,
            _playerHolder.VisibilityMinY,
            _playerHolder.VisibilityMinZ
        );
        Vector3 visibilitySize = new Vector3(
            _playerHolder.VisibilityWidth,
            _playerHolder.VisibilityDepth,
            _playerHolder.VisibilityHeight
        );

        if (visibilitySize.X <= 0 || visibilitySize.Y <= 0 || visibilitySize.Z <= 0)
            return;

        SetFogParameters(FowMaterial, gridOrigin, cellSizeWorld, visibilityMin, visibilitySize);

        foreach (var material in AdditionalFogMaterials)
        {
            if (material != null && material != FowMaterial)
                SetFogParameters(material, gridOrigin, cellSizeWorld, visibilityMin, visibilitySize);
        }

        if (DebugMode)
        {
            GD.Print(
                $"FogManager: updated {visibilitySize} visibility grid at {visibilityMin}; " +
                $"origin {gridOrigin}, cell size {cellSizeWorld}."
            );
        }
    }

    private void SetFogParameters(
        ShaderMaterial material,
        Vector3 gridOrigin,
        Vector3 cellSizeWorld,
        Vector3 visibilityMin,
        Vector3 visibilitySize
    )
    {
        material.SetShaderParameter("visibility_texture", _visibilityTexture3DCache);
        material.SetShaderParameter("grid_origin_world", gridOrigin);
        material.SetShaderParameter("cell_size_world", cellSizeWorld);
        material.SetShaderParameter("visibility_grid_min", visibilityMin);
        material.SetShaderParameter("visibility_grid_size", visibilitySize);
    }

    public override void Deinitialize()
    {
        if (GridObjectManager.Instance != null)
        {
            if (_playerHolder != null)
            {
                _playerHolder.VisibilityChanged -= OnVisibilityChanged;
            }
        }

        _playerHolder = null;
    }

    #region Manager Data (Save/Load - Not needed for Fog visual state usually)
    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        return new Godot.Collections.Dictionary<string, Variant>();
    }

    public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        InitFogGlobals();
        return Task.CompletedTask;
    }
    #endregion
}
