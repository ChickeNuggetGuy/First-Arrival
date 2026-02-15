using Godot;

namespace FirstArrival.Scripts.UI;

[GlobalClass]
public partial class GridPathVisual : Node3D
{
	[Export] private MeshInstance3D mesh;
	[Export] private Label3D costLabel;

	// Offset above the grid cell surface
	private const float Y_OFFSET = 0.05f;

	public void Setup(
		Vector3 worldPosition,
		Vector3? lookAtTarget,
		int remainingTimeUnits,
		int remainingStamina,
		bool isReachable
	)
	{
		GlobalPosition = worldPosition + Vector3.Up * Y_OFFSET;

		// Rotate arrow to face the next cell in the path
		if (lookAtTarget.HasValue)
		{
			var target = lookAtTarget.Value + Vector3.Up * Y_OFFSET;
			var direction = (target - GlobalPosition).Normalized();

			// Only rotate if there's meaningful horizontal distance
			var flat = new Vector3(direction.X, 0, direction.Z);
			if (flat.LengthSquared() > 0.001f)
			{
				LookAt(GlobalPosition + flat, Vector3.Up);
			}
		}

		// Update label
		if (costLabel != null)
		{
			costLabel.Text = $"TU: {remainingTimeUnits}\nST: {remainingStamina}";
			costLabel.Modulate = isReachable
				? new Color(1f, 1f, 1f)
				: new Color(1f, 0.3f, 0.3f);
		}

		// Dim the arrow mesh itself if unreachable
		var mat = mesh.GetActiveMaterial(0);
		if (mat is StandardMaterial3D stdMat)
		{
			var overlay = (StandardMaterial3D)stdMat.Duplicate();
			overlay.AlbedoColor = isReachable
				? new Color(0.2f, 0.6f, 1f, 0.8f)
				: new Color(1f, 0.2f, 0.2f, 0.5f);
			mesh.MaterialOverride = overlay;
		}

		Visible = true;
	}

	public void Hide()
	{
		Visible = false;
	}
}