using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
[Tool]
public partial class FogOfWarManager : Manager<FogOfWarManager>
{
  [Export] public MeshInstance3D fogMesh; // Unseen overlay quad (fullscreen)
  [Export] public Color UnseenColor = Colors.Black;
  [Export(PropertyHint.Range, "0,1,0.01")]
  public float UnseenOpacity = 0.95f;

  [Export] public bool EnableExploredOverlay = true;
  [Export] public Color ExploredColor = new Color(0.1f, 0.1f, 0.1f);
  [Export(PropertyHint.Range, "0,1,0.01")]
  public float ExploredOpacity = 0.45f;

  // Thickness scale of per-cell visibility volumes along Y
  [Export(PropertyHint.Range, "0,2,0.01")]
  public float VolumeYThicknessFactor = 0.98f;

  // Optional: used only for saving to disk with F5 (debug)
  [Export] public ImageTexture fogTexture;

  private GridObjectTeamHolder _playerTeamHolder;

  // Per-cell volumes used to WRITE stencil refs (1 = visible, 2 = explored)
  private MultiMeshInstance3D _visibleVolumes;
  private MultiMeshInstance3D _exploredVolumes;

  // Optional second overlay quad for "explored" tint
  private MeshInstance3D _fogOverlayExplored;

  public override string GetManagerName() => "FogOfWarManager";

  protected override Task _Setup(bool loadingData)
  {
    _playerTeamHolder = GridObjectManager.Instance
      .GetGridObjectTeamHolder(Enums.UnitTeam.Player);

    EnsureFowNodes();
    return Task.CompletedTask;
  }

  protected override Task _Execute(bool loadingData)
  {
    if (CameraController.Instance != null)
    {
      CameraController.Instance.CameraYLevelChanged +=
        InstanceOnCameraYLevelChanged;
    }

    // Listen to visibility updates to rebuild the volume instances
    if (_playerTeamHolder != null)
    {
      _playerTeamHolder.Connect(
        GridObjectTeamHolder.SignalName.VisibilityChanged,
        new Callable(this, nameof(OnVisibilityChanged))
      );
    }

    // Initial build (if visibility already computed)
    RebuildFowVolumes();

    return Task.CompletedTask;
  }

  public override void _Process(double delta)
  {
    // Keep the fullscreen overlay(s) in front of the camera
    UpdateFullScreenOverlays();

    // Optional: keep a handle to current Y layer texture for debug saving
    if (_playerTeamHolder != null && CameraController.Instance != null)
    {
      int y = CameraController.Instance.CurrentYLevel;
      if (_playerTeamHolder.VisibilityTextures != null
          && _playerTeamHolder.VisibilityTextures.TryGetValue(y, out var tex)
          && tex != null)
      {
        fogTexture = tex;
      }
    }
  }

  private void InstanceOnCameraYLevelChanged(CameraController controller)
  {
    RebuildFowVolumes();
  }

  private void OnVisibilityChanged(GridObjectTeamHolder holder)
  {
    RebuildFowVolumes();
  }

  // Build/ensure nodes and materials (volumes + overlays)
  private void EnsureFowNodes()
  {
    // 1) Overlay(s): fogMesh for UNSEEN; optional explored overlay
    if (fogMesh == null)
    {
      fogMesh = new MeshInstance3D { Name = "FOW_Overlay_Unseen" };
      fogMesh.Mesh = new QuadMesh();
      AddChild(fogMesh);
    }
    else if (fogMesh.Mesh is not QuadMesh)
    {
      fogMesh.Mesh = new QuadMesh();
    }

    fogMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    fogMesh.MaterialOverride = MakeOverlayMaterialEqualRef(
      UnseenColor,
      UnseenOpacity,
      refValue: 0 // draw only where stencil == 0 (unseen)
    );

    if (EnableExploredOverlay)
    {
      if (_fogOverlayExplored == null)
      {
        _fogOverlayExplored = new MeshInstance3D
        {
          Name = "FOW_Overlay_Explored"
        };
        _fogOverlayExplored.Mesh = new QuadMesh();
        _fogOverlayExplored.CastShadow =
          GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_fogOverlayExplored);
      }
      else if (_fogOverlayExplored.Mesh is not QuadMesh)
      {
        _fogOverlayExplored.Mesh = new QuadMesh();
      }

      _fogOverlayExplored.MaterialOverride = MakeOverlayMaterialEqualRef(
        ExploredColor,
        ExploredOpacity,
        refValue: 2 // draw where stencil == 2 (explored)
      );
    }

    // 2) Volume writers
    if (_visibleVolumes == null)
    {
      _visibleVolumes = CreateVolumeMMI(
        "FOW_VisibleVolumes",
        MakeStencilWriteMaterial(refValue: 1)
      );
    }

    if (_exploredVolumes == null)
    {
      _exploredVolumes = CreateVolumeMMI(
        "FOW_ExploredVolumes",
        MakeStencilWriteMaterial(refValue: 2)
      );
    }
  }

  // Writes a stencil reference, fully invisible draw (no color)
  private StandardMaterial3D MakeStencilWriteMaterial(int refValue)
  {
    var m = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      AlbedoColor = new Color(0, 0, 0, 0),
      CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      NoDepthTest = true,
      DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
      RenderPriority = 0
    };

    m.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
    m.StencilReference = refValue;
    // m.StencilReadMask = 0xFF;
    // m.StencilWriteMask = 0xFF;
    m.StencilCompare = BaseMaterial3D.StencilCompareEnum.Always;

    return m;
  }

  // Overlay draws where stencil == refValue
  private StandardMaterial3D MakeOverlayMaterialEqualRef(
    Color color,
    float opacity,
    int refValue
  )
  {
    var m = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass,
      AlbedoColor = new Color(color.R, color.G, color.B, opacity),
      CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      NoDepthTest = true,
      DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
      RenderPriority = 127 // draw last
    };

    m.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
    m.StencilReference = refValue;
    m.StencilFlags = (int)BaseMaterial3D.StencilFlagsEnum.Read;
    m.StencilCompare = BaseMaterial3D.StencilCompareEnum.Equal;


    return m;
  }

  // Create a MultiMeshInstance3D with a BoxMesh and a material
  private MultiMeshInstance3D CreateVolumeMMI(
    string name,
    Material materialOverride
  )
  {
    var mmi = new MultiMeshInstance3D { Name = name };
    mmi.Multimesh = new MultiMesh
    {
      TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
      InstanceCount = 0,
      UseColors = false,
      UseCustomData = false
    };
    // Assign a BoxMesh in a version-safe way
    var box = new BoxMesh();
    AssignMeshToMMI(mmi, box);

    mmi.MaterialOverride = materialOverride;
    mmi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    AddChild(mmi);
    return mmi;
  }

  // Version-safe mesh assignment for MultiMeshInstance3D
  private static void AssignMeshToMMI(
    MultiMeshInstance3D mmi,
    Resource meshRes
  )
  {
    ((GodotObject)mmi).Set("mesh", meshRes);
    if (mmi.Multimesh != null)
    {
      ((GodotObject)mmi.Multimesh).Set("mesh", meshRes);
    }
  }

  // Build per-cell volumes for current Y level
  private void RebuildFowVolumes()
  {
    if (_playerTeamHolder == null) return;
    var terrain = MeshTerrainGenerator.Instance;
    if (terrain == null) return;

    Vector2 cs2 = terrain.GetCellSize();
    Vector3 cell = new Vector3(cs2.X, cs2.Y, cs2.X);

    int yLevel = CameraController.Instance != null
      ? CameraController.Instance.CurrentYLevel
      : 0;

    // World origin for grid (adjust if your grid is offset)
    Vector3 worldOrigin = Vector3.Zero;

    // Visible and explored sets
    List<GridCell> visibleAll = _playerTeamHolder.GetVisibleGridCells();
    var visibleLayer = visibleAll
      .Where(c => c.gridCoordinates.Y == yLevel)
      .ToList();

    var exploredLayer = _playerTeamHolder.ExploredCells
      .Where(c => c.gridCoordinates.Y == yLevel)
      .ToList();

    // Visible volumes (ref=1)
    if (_visibleVolumes?.Multimesh != null)
    {
      var mm = _visibleVolumes.Multimesh;
      mm.InstanceCount = visibleLayer.Count;
      for (int i = 0; i < visibleLayer.Count; i++)
      {
        var g = visibleLayer[i].gridCoordinates;
        Vector3 center = worldOrigin
          + new Vector3(
            (g.X + 0.5f) * cell.X,
            (g.Y + 0.5f) * cell.Y,
            (g.Z + 0.5f) * cell.Z
          );

        Vector3 scale =
          new Vector3(cell.X, cell.Y * VolumeYThicknessFactor, cell.Z);

        var basis = Basis.Identity.Scaled(scale);
        var t = new Transform3D(basis, center);
        mm.SetInstanceTransform(i, t);
      }
    }

    // Explored-only volumes (ref=2): explored minus visible
    if (_exploredVolumes?.Multimesh != null)
    {
      var exploredOnly = exploredLayer
        .Where(c => !visibleLayer.Contains(c))
        .ToList();

      var mm = _exploredVolumes.Multimesh;
      mm.InstanceCount = exploredOnly.Count;
      for (int i = 0; i < exploredOnly.Count; i++)
      {
        var g = exploredOnly[i].gridCoordinates;
        Vector3 center = worldOrigin
          + new Vector3(
            (g.X + 0.5f) * cell.X,
            (g.Y + 0.5f) * cell.Y,
            (g.Z + 0.5f) * cell.Z
          );

        Vector3 scale =
          new Vector3(cell.X, cell.Y * VolumeYThicknessFactor, cell.Z);

        var basis = Basis.Identity.Scaled(scale);
        var t = new Transform3D(basis, center);
        mm.SetInstanceTransform(i, t);
      }
    }

    // Keep overlay materials up to date with exported colors/opacity
    if (fogMesh?.MaterialOverride is StandardMaterial3D unseenMat)
    {
      unseenMat.AlbedoColor =
        new Color(UnseenColor.R, UnseenColor.G, UnseenColor.B, UnseenOpacity);
    }
    if (EnableExploredOverlay
        && _fogOverlayExplored?.MaterialOverride is StandardMaterial3D expMat)
    {
      expMat.AlbedoColor = new Color(
        ExploredColor.R,
        ExploredColor.G,
        ExploredColor.B,
        ExploredOpacity
      );
    }
  }

  // Place overlay quad(s) to tightly cover the screen
  private void UpdateFullScreenOverlays()
  {
    var cam = GetViewport()?.GetCamera3D();
    if (cam == null) return;

    void Place(MeshInstance3D mi)
    {
      if (mi == null) return;

      float near = cam.Near;
      float fovRad = Mathf.DegToRad(cam.Fov);
      float h = 2.0f * Mathf.Tan(fovRad * 0.5f) * near;
      float aspect =
        (float)GetViewport().GetVisibleRect().Size.X
        / (float)GetViewport().GetVisibleRect().Size.Y;
      float w = h * aspect;

      if (mi.Mesh is QuadMesh q)
      {
        q.Size = new Vector2(w * 1.03f, h * 1.03f);
      }

      var xf = cam.GlobalTransform;
      Vector3 fwd = -xf.Basis.Z;
      float push = near + Mathf.Max(0.02f, near * 0.02f);
      Vector3 pos = xf.Origin + fwd * push;
      mi.GlobalTransform = new Transform3D(xf.Basis, pos);
    }

    Place(fogMesh);
    if (EnableExploredOverlay) Place(_fogOverlayExplored);
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event is InputEventKey eventKey && eventKey.Pressed
        && !eventKey.IsEcho())
    {
      if (eventKey.Keycode == Key.F5)
      {
        GD.Print("F5 pressed, attempting to save fog texture.");
        if (fogTexture != null)
        {
          var image = fogTexture.GetImage();
          if (image != null)
          {
            var yLevel = -1;
            if (CameraController.Instance != null)
            {
              yLevel = CameraController.Instance.CurrentYLevel;
            }
            var path =
              $"/Users/malikhawkins/first-arrival/Data/fog_dump_y{yLevel}.png";

            Error err = image.SavePng(path);
            if (err == Error.Ok)
            {
              GD.Print($"Successfully saved fog texture to {path}");
            }
            else
            {
              GD.PrintErr(
                $"Failed to save fog texture to {path}. Error: {err}"
              );
            }
          }
          else
          {
            GD.PrintErr("fogTexture has no image to save.");
          }
        }
        else
        {
          GD.PrintErr("fogTexture is null. Cannot save.");
        }
      }
    }
  }

  #region manager Data
  public override void Load(Godot.Collections.Dictionary<string,Variant> data)
  {
	  base.Load(data);
	  if(!HasLoadedData) return;
  }

  public override Godot.Collections.Dictionary<string,Variant> Save()
  {
    return null;
  }
  #endregion

  public override void Deinitialize()
  {
	  return;
  }
}