using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class RangedAttackActionBase : ActionBase, ICompositeAction, IItemAction
{
	private const float RegularSpreadScale = 0.5f;
	private const float MaximumFlyerDeviationDegrees = 28.4f;
	private const float MinimumFlyerChance = 0.02f;
	private const float MaximumFlyerChance = 0.12f;

	public Item Item { get; set; }
	public ActionBase ParentActionBase { get; set; }
	public List<ActionBase> SubActions { get; set; }

	public RangedAttackActionBase(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell,
		ActionDefinition parentAction,
		Godot.Collections.Dictionary<Enums.Stat, int> costs) : base(parentGridObject, startingGridCell, targetGridCell, parentAction,
		costs)
	{
		if (parentGridObject == null)
			GD.PushError("RangedAttackAction: parentGridObject is null!");
	}

	protected override async Task Setup()
	{
		ParentActionBase = this;
		AddRotateSubActionIfNeeded(startingGridCell, targetGridCell);
		await Task.CompletedTask;
	}

	protected override async Task Execute()
	{
		if (parentGridObject == null)
		{
			GD.PrintErr("Execute: parentGridObject is null");
			return;
		}


		if (!parentGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder parentStatHolder))
		{
			GD.PrintErr("RangedAttack: shooter has no GridObjectStatHolder");
			return;
		}

		RangedAttackActionDefinition rangedAttackActionDefinition =
			parentActionDefinition as RangedAttackActionDefinition;
		if (rangedAttackActionDefinition == null)
		{
			GD.PrintErr("RangedAttack: parent action definition is invalid");
			return;
		}

		if (!parentStatHolder.TryGetStat(Enums.Stat.RangedAccuracy, out GridObjectStat rangedAccuracy))
		{
			GD.PrintErr("RangedAttack: shooter has no RangedAccuracy stat");
			return;
		}

		Vector3 origin = parentGridObject.objectCenter?.GlobalPosition
			?? parentGridObject.GlobalPosition + Vector3.Up;
		if (parentGridObject.objectCenter == null)
			GD.PushWarning("RangedAttack: objectCenter is not assigned; using the GridObject origin instead");

		int damage = rangedAttackActionDefinition.damage;
		GD.Print($"RangedAttack: firing {rangedAttackActionDefinition.attackCount} shot(s) from {origin}");

		for (int i = 0; i < rangedAttackActionDefinition.attackCount; i++)
		{
			Vector3 tweenPos = origin;
			GridObject targetGridObject = null;

			Vector3 direction =
				((targetGridCell.WorldCenter + Vector3.Up) - origin).Normalized();
			float effectiveAccuracy = Mathf.Clamp(
				rangedAccuracy.CurrentValue + rangedAttackActionDefinition.accuracy,
				0f,
				100f
			);
			Vector3 newDirection = CalculateProjectileDirection(direction, effectiveAccuracy);

			if (RaycastCheck(origin, newDirection, out var results))
			{
				tweenPos = results["position"].As<Vector3>();
				GD.Print($"Hit {tweenPos}");

				Node collider = results["collider"].As<Node>();
				targetGridObject = collider as GridObject
					?? collider?.FindParentByTypeRecursive<GridObject>();
			}
			else
			{
				tweenPos = origin + newDirection * 25;
			}

			if (ShouldAnimate())
			{
				var visual = new CsgSphere3D { Radius = 0.125f };
				parentGridObject.GetTree().Root.AddChild(visual);
				visual.GlobalPosition = origin;

				Tween tween = ApplyAnimationSpeed(parentGridObject.CreateTween());
				tween.SetTrans(Tween.TransitionType.Linear);
				tween.SetEase(Tween.EaseType.InOut);
				float tweenDuration = origin.DistanceTo(tweenPos) / 100f;
				tween.TweenProperty(visual, "global_position", tweenPos, tweenDuration);
				await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
				visual.QueueFree();
			}

			if (targetGridObject == null)
			{
				GD.Print("No target hit or target is not a GridObject");
				continue;
			}


			if (!targetGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder targetStatHolder))
				return;

			targetStatHolder.TryRemoveStatCosts(rangedAttackActionDefinition.damagingStats);
			if (!targetStatHolder.TryGetStat(Enums.Stat.Health, out var health))
			{
				GD.Print("Target Grid Object does not have Health stat");
			}
			else if (Item is { ItemData: not null })
			{
				health.RemoveValue(damage);
				GD.Print(
					$"Target unit: {targetGridObject.Name} Damaged for {damage} damage, remaining health is {health.CurrentValue}");
			}
		}
	}


	private Vector3 CalculateProjectileDirection(Vector3 direction, float effectiveAccuracy)
	{
		float normalizedAccuracy = Mathf.Clamp(effectiveAccuracy / 100f, 0f, 1f);
		float flyerChance = Mathf.Lerp(
			MaximumFlyerChance,
			MinimumFlyerChance,
			normalizedAccuracy
		);
		bool isFlyer = GD.Randf() < flyerChance;

		Vector2 regularDeviationMax = mathUtils.GetMaxDeviation(effectiveAccuracy, 1, true)
			* RegularSpreadScale;

		float yawDegrees;
		float pitchDegrees;
		if (isFlyer)
		{
			// Flyers retain the full possible cone at every accuracy level. Accuracy
			// makes them less frequent rather than making them impossible.
			yawDegrees = (float)GD.RandRange(
				-MaximumFlyerDeviationDegrees,
				MaximumFlyerDeviationDegrees
			);
			pitchDegrees = (float)GD.RandRange(
				-MaximumFlyerDeviationDegrees,
				MaximumFlyerDeviationDegrees
			);
		}
		else
		{
			// The difference of two uniform samples creates a triangular distribution,
			// keeping ordinary shots near the aim point while retaining soft tails.
			yawDegrees = SampleTriangularDeviation(regularDeviationMax.X);
			pitchDegrees = SampleTriangularDeviation(regularDeviationMax.Y);
		}

		float yaw = Mathf.DegToRad(yawDegrees);
		float pitch = Mathf.DegToRad(pitchDegrees);

		Quaternion yawRotation = new Quaternion(Vector3.Up, yaw);

		Vector3 right = yawRotation * Vector3.Right;

		Quaternion pitchRotation = new Quaternion(right, pitch);

		Quaternion finalRotation = yawRotation * pitchRotation;

		return finalRotation * direction.Normalized();
	}

	private static float SampleTriangularDeviation(float maximumDeviation)
	{
		return (GD.Randf() - GD.Randf()) * maximumDeviation;
	}

	private bool RaycastCheck(Vector3 startPoint, Vector3 direction, out Godot.Collections.Dictionary result)
	{
		result = new Godot.Collections.Dictionary();

		if (parentGridObject == null)
		{
			GD.PrintErr("RaycastCheck: parentGridObject is null");
			return false;
		}

		var world3D = parentGridObject.GetWorld3D();
		if (world3D == null)
		{
			GD.PrintErr("RaycastCheck: GetWorld3D() returned null");
			return false;
		}

		var spaceState = world3D.DirectSpaceState;
		if (spaceState == null)
		{
			GD.PrintErr("RaycastCheck: DirectSpaceState is null");
			return false;
		}

		Vector3I mapSize =
			MeshTerrainGenerator.Instance?.GetMapCellSize() ?? new Vector3I(100, 100, 100); // fallback size
		float maxRayDistance = (mapSize.X > mapSize.Z) ? mapSize.X : mapSize.Z;

		var endPoint = startPoint + (direction * maxRayDistance);
		var query = PhysicsRayQueryParameters3D.Create(startPoint, endPoint);
		query.CollideWithAreas = true;
		query.CollideWithBodies = true;


		query.Exclude = new Godot.Collections.Array<Rid> { parentGridObject.GetRid() };

		result = spaceState.IntersectRay(query);

		if (result.Count > 0)
		{
			GD.Print("Hit at point: ", result["position"]);
			DebugDraw3D.DrawLine(startPoint, result["position"].As<Vector3>(), Colors.Yellow, 10);
			return true;
		}
		else
		{
			DebugDraw3D.DrawLine(startPoint, endPoint, Colors.Red, 10);
			GD.Print("Nothing hit");
		}

		return false;
	}


	protected override async Task ActionComplete()
	{
		return;
	}
}
