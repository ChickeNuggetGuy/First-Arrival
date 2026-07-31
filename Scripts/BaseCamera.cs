using Godot;
using System.Collections.Generic;

public partial class BaseCamera : Camera3D
{
	public static BaseCamera Instance {get; private set;}
	[ExportCategory("Movement")]
	[Export(PropertyHint.Range, "0,100,0.5,or_greater")]
	public float MoveSpeed { get; set; } = 12.0f;

	[ExportCategory("Zoom")]
	[Export(PropertyHint.Range, "0.1,20,0.1,or_greater")]
	public float ZoomStep { get; set; } = 2.0f;

	[Export(PropertyHint.Range, "0.1,100,0.1,or_greater")]
	public float MinZoomDistance { get; set; } = 3.0f;

	[Export(PropertyHint.Range, "1,500,1,or_greater")]
	public float MaxZoomDistance { get; set; } = 80.0f;

	[Export]
	public float MovementPlaneY { get; set; } = 0.0f;

	[ExportCategory("Focus")]
	[Export(PropertyHint.Range, "1,3,0.05")]
	public float FocusPadding { get; set; } = 1.25f;

	[Export(PropertyHint.Range, "0.1,20,0.1,or_greater")]
	public float MinimumFocusSize { get; set; } = 1.0f;


	public override void _EnterTree()
	{
		base._EnterTree();
		if (Instance == null || !GodotObject.IsInstanceValid(Instance))
		{
			Instance = this;
		}
		else if (Instance != this)
		{
			QueueFree();
		}
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;

		base._ExitTree();
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 input = Input.GetVector(
			"cameraLeft",
			"cameraRight",
			"cameraUp",
			"cameraDown");

		if (input.IsZeroApprox())
			return;

		GetPlanarDirections(out Vector3 right, out Vector3 forward);
		Vector3 movement = right * input.X - forward * input.Y;

		if (movement.LengthSquared() > 1.0f)
			movement = movement.Normalized();

		GlobalPosition += movement * MoveSpeed * (float)delta;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { Pressed: true } mouseButton)
			return;

		float zoomSteps = mouseButton.ButtonIndex switch
		{
			MouseButton.WheelUp => mouseButton.Factor,
			MouseButton.WheelDown => -mouseButton.Factor,
			_ => 0.0f,
		};

		if (Mathf.IsZeroApprox(zoomSteps))
			return;

		ZoomBy(zoomSteps);
		GetViewport().SetInputAsHandled();
	}

	/// <summary>
	/// Moves the camera closer to (positive steps) or farther from (negative
	/// steps) the horizontal movement plane.
	/// </summary>
	public void ZoomBy(float steps)
	{
		Vector3 viewDirection = GetViewDirection();

		if (!TryGetDistanceToMovementPlane(viewDirection, out float currentDistance))
		{

			GlobalPosition += viewDirection * steps * ZoomStep;
			return;
		}

		float minimum = Mathf.Max(MinZoomDistance, 0.1f);
		float maximum = Mathf.Max(MaxZoomDistance, minimum);
		float newDistance = Mathf.Clamp(
			currentDistance - steps * ZoomStep,
			minimum,
			maximum);

		Vector3 pointOnPlane = GlobalPosition + viewDirection * currentDistance;
		GlobalPosition = pointOnPlane - viewDirection * newDistance;
	}

	/// <summary>
	/// Centers and frames a node using the bounds of its visible 3D children.
	/// Nodes without visible geometry are focused using their global position.
	/// </summary>
	public void FocusOn(Node3D target)
	{
		if (target == null || !GodotObject.IsInstanceValid(target))
			return;

		FocusOnNodes(new[] { target });
	}

	/// <summary>
	/// Centers and frames several nodes as a single subject.
	/// </summary>
	public void FocusOn(Node3D[] targets)
	{
		FocusOnNodes(targets);
	}

	/// <summary>
	/// Centers and frames every Node3D in a Godot scene-tree group.
	/// </summary>
	public void FocusOnGroup(StringName groupName)
	{
		var targets = new List<Node3D>();

		foreach (Node node in GetTree().GetNodesInGroup(groupName))
		{
			if (node is Node3D target)
				targets.Add(target);
		}

		FocusOn(targets.ToArray());
	}

	private void FocusOnNodes(IEnumerable<Node3D> targets)
	{
		if (!TryGetCombinedBounds(targets, out Aabb bounds))
			return;

		Vector3 center = bounds.GetCenter();
		Vector3 viewDirection = GetViewDirection();
		float minimum = Mathf.Max(MinZoomDistance, 0.1f);
		float maximum = Mathf.Max(MaxZoomDistance, minimum);
		float distance;

		if (Projection == ProjectionType.Orthogonal)
		{
			float diameter = Mathf.Max(bounds.Size.Length(), MinimumFocusSize);
			Size = diameter * Mathf.Max(FocusPadding, 1.0f) * GetAspectCorrection();

			distance = TryGetDistanceToMovementPlane(viewDirection, out float currentDistance)
				? Mathf.Clamp(currentDistance, minimum, maximum)
				: minimum;
		}
		else
		{
			float radius = Mathf.Max(bounds.Size.Length() * 0.5f, MinimumFocusSize * 0.5f);
			float halfFov = Mathf.DegToRad(Mathf.Clamp(Fov, 1.0f, 179.0f) * 0.5f);
			float requiredDistance =
				radius
				* Mathf.Max(FocusPadding, 1.0f)
				* GetAspectCorrection()
				/ Mathf.Max(Mathf.Tan(halfFov), 0.001f);

			distance = Mathf.Clamp(requiredDistance, minimum, maximum);
		}
		
		GlobalPosition = center - viewDirection * distance;
	}

	private bool TryGetCombinedBounds(IEnumerable<Node3D> targets, out Aabb bounds)
	{
		bounds = default;
		bool foundAnyTarget = false;

		foreach (Node3D target in targets)
		{
			if (target == null || !GodotObject.IsInstanceValid(target))
				continue;

			bool foundTargetVisual = false;

			if (target is VisualInstance3D targetVisual && targetVisual.Visible)
			{
				AddVisualBounds(targetVisual, ref bounds, ref foundAnyTarget);
				foundTargetVisual = true;
			}

			foreach (Node child in target.FindChildren(
				"*",
				nameof(VisualInstance3D),
				true,
				false))
			{
				if (child is not VisualInstance3D visual || !visual.Visible)
					continue;

				AddVisualBounds(visual, ref bounds, ref foundAnyTarget);
				foundTargetVisual = true;
			}

			if (!foundTargetVisual)
				AddBounds(new Aabb(target.GlobalPosition, Vector3.Zero), ref bounds, ref foundAnyTarget);
		}

		return foundAnyTarget;
	}

	private static void AddVisualBounds(
		VisualInstance3D visual,
		ref Aabb bounds,
		ref bool foundAnyBounds)
	{
		AddBounds(
			visual.GlobalTransform * visual.GetAabb(),
			ref bounds,
			ref foundAnyBounds);
	}

	private static void AddBounds(Aabb next, ref Aabb bounds, ref bool foundAnyBounds)
	{
		bounds = foundAnyBounds ? bounds.Merge(next) : next;
		foundAnyBounds = true;
	}

	private void GetPlanarDirections(out Vector3 right, out Vector3 forward)
	{
		right = GlobalBasis.X;
		right.Y = 0.0f;

		forward = -GlobalBasis.Z;
		forward.Y = 0.0f;

		// A straight-down camera has no horizontal viewing direction. In that
		// case, its local up direction represents "up" on the player's screen.
		if (forward.LengthSquared() < 0.000001f)
		{
			forward = GlobalBasis.Y;
			forward.Y = 0.0f;
		}

		forward = forward.LengthSquared() < 0.000001f
			? Vector3.Forward
			: forward.Normalized();

		right = right.LengthSquared() < 0.000001f
			? forward.Cross(Vector3.Up).Normalized()
			: right.Normalized();
	}

	private Vector3 GetViewDirection()
	{
		Vector3 direction = -GlobalBasis.Z;
		return direction.IsZeroApprox() ? Vector3.Forward : direction.Normalized();
	}

	private bool TryGetDistanceToMovementPlane(
		Vector3 viewDirection,
		out float distance)
	{
		distance = 0.0f;

		if (Mathf.Abs(viewDirection.Y) < 0.0001f)
			return false;

		distance = (MovementPlaneY - GlobalPosition.Y) / viewDirection.Y;
		return distance >= 0.0f;
	}

	private float GetAspectCorrection()
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float aspect = viewportSize.X / Mathf.Max(viewportSize.Y, 1.0f);
		return 1.0f / Mathf.Min(aspect, 1.0f);
	}
}
