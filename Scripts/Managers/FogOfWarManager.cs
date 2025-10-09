
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
[Tool]
public partial class FogOfWarManager : Manager<FogOfWarManager>
{
	
	[Export] MeshInstance3D fogMesh;
	[Export] ImageTexture fogTexture;
	protected override Task _Setup()
	{
		return Task.CompletedTask;
	}

	public override void _Process(double delta)
	{
		if (fogMesh == null) return;
		var fogMaterial = fogMesh.GetActiveMaterial(0) as ShaderMaterial;
		if (fogMaterial == null) return;
		
		//fogMaterial.SetShaderParameter("fog_texture", fogTexture);

		var terrainGenerator = MeshTerrainGenerator.Instance;
		if (terrainGenerator != null)
		{
			Vector2 gridCellSize2D = terrainGenerator.GetCellSize();
			Vector3 gridCellSize = new Vector3(gridCellSize2D.X, gridCellSize2D.Y, gridCellSize2D.X);
			Vector3I mapCellSize = terrainGenerator.GetMapCellSize();

			fogMaterial.SetShaderParameter("grid_origin", Vector3.Zero);
			fogMaterial.SetShaderParameter("grid_cell_size", gridCellSize);

			Vector3 worldSize = new Vector3(
				mapCellSize.X * gridCellSize.X,
				mapCellSize.Y * gridCellSize.Y,
				mapCellSize.Z * gridCellSize.Z
			);
			
			Vector3 gridSizeInv = new Vector3(
				Mathf.IsZeroApprox(worldSize.X) ? 0 : 1.0f / worldSize.X,
				Mathf.IsZeroApprox(worldSize.Y) ? 0 : 1.0f / worldSize.Y,
				Mathf.IsZeroApprox(worldSize.Z) ? 0 : 1.0f / worldSize.Z
			);

			fogMaterial.SetShaderParameter("grid_size_inv", gridSizeInv);
		}
	}

	protected override  Task _Execute()
	{
		CameraController.Instance.CameraYLevelChanged += InstanceOnCameraYLevelChanged;
		return Task.CompletedTask;
	}

	private void InstanceOnCameraYLevelChanged(CameraController cameraController)
	{
		var fogMaterial = fogMesh?.GetActiveMaterial(0) as ShaderMaterial;
    	if (fogMaterial == null) return;

		GridObjectTeamHolder playerTeam = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
		if (playerTeam == null)return;

		fogTexture = playerTeam.VisibilityTextures[cameraController.CurrentYLevel];
		//fogMaterial.SetShaderParameter("fog_texture", fogTexture);
	}
	
	public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed && !eventKey.IsEcho())
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
                        var path = $"/Users/malikhawkins/first-arrival/Data/fog_dump_y{yLevel}.png";
                        
                        Error err = image.SavePng(path);
                        if (err == Error.Ok)
                        {
                            GD.Print($"Successfully saved fog texture to {path}");
                        }
                        else
                        {
                            GD.PrintErr($"Failed to save fog texture to {path}. Error: {err}");
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
	protected override void GetInstanceData(ManagerData data)
	{
		GD.Print("No data to transfer");
	}

	public override ManagerData SetInstanceData()
	{
		return null;
	}
	#endregion
	
}
