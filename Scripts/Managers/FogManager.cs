using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class FogManager : Manager<FogManager>
{
    [Export] public ShaderMaterial FowMaterial { get; set; }
	
    private ImageTexture3D _visibilityTexture3DCache;

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
        
        var playerHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
        if (playerHolder == null)
        {
            GD.PrintErr("FogManager: Player GridObjectTeamHolder not found!");
            return;
        }
        
        playerHolder.VisibilityChanged -= OnVisibilityChanged;
        playerHolder.VisibilityChanged += OnVisibilityChanged;
        
        Vector3I mapSize = MeshTerrainGenerator.Instance.GetMapCellSize();
        
        Vector2 cellSize = MeshTerrainGenerator.Instance.cellSize; 

        var playerTexture = playerHolder.VisibilityTexture3D;

        if (playerTexture == null)
        {
            GD.Print("FogManager: Player visibility texture is not yet initialized.");
            return;
        }

        GD.Print("--- FOG MANAGER INIT ---");
        GD.Print($"Texture Resolution: {mapSize}");
        GD.Print($"Cell Size (World): {cellSize}");
        GD.Print("------------------------");
        
        SetGlobalVisibilityTexture(playerTexture);
        
        RenderingServer.GlobalShaderParameterSet("texture_resolution", (Vector3)mapSize);
        RenderingServer.GlobalShaderParameterSet("cell_size_world", cellSize);
        RenderingServer.GlobalShaderParameterSet("grid_origin_world", Vector3.Zero);
    }

    /// <summary>
    /// Event handler for when any team updates their visibility.
    ///  filter this to ONLY update the shader if it's the Player team.
    /// </summary>
    private void OnVisibilityChanged(Enums.UnitTeam team, ImageTexture3D texture)
    {
        if (team == Enums.UnitTeam.Player)
        {
            SetGlobalVisibilityTexture(texture);
        }
    }

    private void SetGlobalVisibilityTexture(ImageTexture3D texture)
    {
        _visibilityTexture3DCache = texture;
        
        if (_visibilityTexture3DCache != null)
        {
            RenderingServer.GlobalShaderParameterSet("visibility_grid_texture", _visibilityTexture3DCache);
            
            if (FowMaterial != null)
            {
                FowMaterial.SetShaderParameter("visibility_texture", _visibilityTexture3DCache);
            }
        }
    }

    public override void Deinitialize()
    {
        // Cleanup event subscriptions
        if (GridObjectManager.Instance != null)
        {
            var playerHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
            if (playerHolder != null)
            {
                playerHolder.VisibilityChanged -= OnVisibilityChanged;
            }
        }
    }

    #region Manager Data (Save/Load - Not needed for Fog visual state usually)
    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        return new Godot.Collections.Dictionary<string, Variant>();
    }

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        base.Load(data);
        // Fog state is usually recalculated by the GridObjectTeamHolder on load,
        // so we just re-init the connections here.
        InitFogGlobals();
    }
    #endregion
}