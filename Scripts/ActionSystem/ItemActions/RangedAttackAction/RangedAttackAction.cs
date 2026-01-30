using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class RangedAttackAction : Action, IItemAction
{
    public Item Item { get; set; }

    public RangedAttackAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, ActionDefinition parentAction,
        Dictionary<Enums.Stat, int> costs) : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
    {
        if (parentGridObject == null)
            GD.PushError("RangedAttackAction: parentGridObject is null!");
    }

    protected override async Task Setup()
    {
        // No setup needed for a simple ranged attack
        return;
    }

    protected override async Task Execute()
    {
        if (parentGridObject == null || parentGridObject.objectCenter == null)
        {
            GD.PrintErr("Execute: parentGridObject or objectCenter is null");
            return;
        }
        
        
        
        if(!parentGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder parentStatHolder)) return;


        GridObjectStat rangedAccuracy = parentStatHolder.Stats.First(stat =>
        {
	        if (stat is not GridObjectStat gridObjectStat) return false;
	        if(gridObjectStat.Stat != Enums.Stat.RangedAccuracy)return false;
	        return true;
        });

        var visual = new CsgSphere3D { Radius = 0.5f };
        parentGridObject.GetTree().Root.AddChild(visual);
        visual.GlobalPosition = parentGridObject.objectCenter.GlobalPosition;

        Tween tween = parentGridObject.CreateTween();
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.SetEase(Tween.EaseType.InOut);

        Vector3 tweenPos = Vector3.Zero;
        float tweenDuration = 0.5f;
        GridObject targetGridObject = null;

        Vector3 direction = ((targetGridCell.worldCenter  + Vector3.Up) - parentGridObject.objectCenter.GlobalPosition).Normalized();
        Vector2 deviationMax = mathUtils.GetMaxDeviation(rangedAccuracy.CurrentValue, 1, true);
        Vector3 newDirection = CalculateProjectileDirection(direction, deviationMax.X, deviationMax.Y);

        if (RaycastCheck(parentGridObject.objectCenter.GlobalPosition, newDirection, out var results))
        {
            tweenPos = results["position"].As<Vector3>();
            GD.Print($"Hit {tweenPos}");

            if (results["collider"].As<Node>() is GridObject gridObject )
            {
                targetGridObject = gridObject;
            }
            else if (results["collider"].As<Node>().GetParent() is GridObject parent)
            {
	            targetGridObject = parent;
            }
        }
        else
        {
            tweenPos = parentGridObject.objectCenter.Position + newDirection * 50;
        }

        tweenDuration = tweenPos.DistanceTo(parentGridObject.objectCenter.Position) / 50;
        tween.TweenProperty(visual, "position", tweenPos, tweenDuration);
        await parentGridObject.ToSignal(tween, Tween.SignalName.Finished);
        visual.QueueFree();

        if (targetGridObject == null)
        {
            GD.Print("No target hit or target is not a GridObject");
            return;
        }
        
        int damage;

	    RangedAttackActionDefinition rangedAttackActionDefinition = parentActionDefinition as RangedAttackActionDefinition;
	    damage = rangedAttackActionDefinition.damage;
        
        
        if(!parentGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder targetStatHolder)) return;

        if (!targetStatHolder.TryGetStat(Enums.Stat.Health, out var health))
        {
            GD.Print("Target Grid Object does not have Health stat");
        }
        else if (Item != null && Item.ItemData != null && Item.ItemData.ItemSettings.HasFlag(Enums.ItemSettings.CanRanged))
        {
            health.RemoveValue(damage);
            GD.Print($"Target unit: {targetGridObject} Damaged for {damage} damage, remaining health is {health.CurrentValue}");
        }
    }


    private Vector3 CalculateProjectileDirection(Vector3 direction, float horizontalDeviationMax, float verticalDeviationMax)
    {
	    float yaw = Mathf.DegToRad((float)GD.RandRange(-horizontalDeviationMax, horizontalDeviationMax));  
	    float pitch = Mathf.DegToRad((float)GD.RandRange(-verticalDeviationMax, verticalDeviationMax));
	    
	    Quaternion yawRotation = new Quaternion(Vector3.Up, yaw);
	    
	    Vector3 right = yawRotation * Vector3.Right;
	    
	    Quaternion pitchRotation = new Quaternion(right, pitch);
	    
	    Quaternion finalRotation = yawRotation * pitchRotation;
	    
	    return finalRotation * direction.Normalized();
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

        Vector3I mapSize = MeshTerrainGenerator.Instance?.GetMapCellSize() ?? new Vector3I(100,100, 100); // fallback size
        float maxRayDistance = (mapSize.X > mapSize.Z) ? mapSize.X  : mapSize.Z;

        var endPoint = startPoint + (direction * maxRayDistance);
        var query = PhysicsRayQueryParameters3D.Create(startPoint, endPoint);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
       

        if (parentGridObject.collisionShape != null)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { parentGridObject.collisionShape.GetRid() };
        }
        else
        {
            GD.PrintErr("collisionShape is null, raycast exclusion disabled");
            query.Exclude = new Godot.Collections.Array<Rid>();
        }

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