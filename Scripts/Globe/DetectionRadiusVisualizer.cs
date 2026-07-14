using Godot;

/// <summary>Builds a globe-hugging overlay from the cells inside a detector's step range.</summary>
public static class DetectionRadiusVisualizer
{
	private const string OverlayName = "DetectionRadiusOverlay";

	public static void AttachOrUpdate(
		Node3D owner,
		int cellIndex,
		int radius,
		Color color,
		bool visible)
	{
		if (owner == null || !GodotObject.IsInstanceValid(owner)) return;

		MeshInstance3D overlay = owner.GetNodeOrNull<MeshInstance3D>(OverlayName);
		if (!visible || radius <= 0)
		{
			if (overlay != null) overlay.Visible = false;
			return;
		}

		GlobeHexGridManager grid = GlobeHexGridManager.Instance;
		HexCellData? origin = grid?.GetCellFromIndex(cellIndex);
		if (!origin.HasValue) return;

		if (overlay == null)
		{
			overlay = new MeshInstance3D { Name = OverlayName, TopLevel = true };
			owner.AddChild(overlay);
			overlay.GlobalTransform = Transform3D.Identity;

			var material = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = color,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				NoDepthTest = false
			};
			overlay.MaterialOverride = material;
		}

		overlay.Visible = true;
		var mesh = new ImmediateMesh();
		mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		foreach (HexCellData cell in grid.GetCellsInStepRange(origin.Value, radius))
		{
			if (cell.Corners == null || cell.Corners.Length < 3) continue;
			Vector3 center = cell.Center * 1.003f;
			for (int i = 0; i < cell.Corners.Length; i++)
			{
				mesh.SurfaceAddVertex(center);
				mesh.SurfaceAddVertex(cell.Corners[i] * 1.003f);
				mesh.SurfaceAddVertex(cell.Corners[(i + 1) % cell.Corners.Length] * 1.003f);
			}
		}
		mesh.SurfaceEnd();
		overlay.Mesh = mesh;
	}
}
