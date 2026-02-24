using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.ItemActions;

public partial class ExplodeAction : Action, ICompositeAction, IDelayedAction, IItemAction
{
    public Action ParentAction { get; set; }
    public List<Action> SubActions { get; set; } = new();
    public Item Item { get; set; }
    
    public int TurnsRemaining { get; set; }
    private int explosionRadius;
    private CsgSphere3D grenadeVisual;
    private float grenadeDamage;

    public ExplodeAction() { }

    public ExplodeAction(
        GridObject parentGridObject,
        GridCell startingGridCell,
        GridCell targetGridCell,
        ActionDefinition parent,
        Item item,
        Godot.Collections.Dictionary<Enums.Stat, int> costs,
        int turnsUntilExplode = 2,
        int radius = 2,
        float damage = 50
    ) : base(parentGridObject, startingGridCell, targetGridCell, parent, costs)
    {
        Item = item;
        TurnsRemaining = turnsUntilExplode;
        explosionRadius = radius;
        grenadeDamage = damage;
    }


    protected override Task Setup()
    {
	    ThrowActionDefinition throwActionDef = null;
	    
	    if (Item?.ItemData != null)
	    {
		    throwActionDef = Item.ItemData.ActionDefinitions?
			    .OfType<ThrowActionDefinition>()
			    .FirstOrDefault();
	    }
	    
	    if (throwActionDef == null && parentGridObject.TryGetGridObjectNode<GridObjectActions>(out var unitActions))
	    {
		    throwActionDef = unitActions.ActionDefinitions?
			    .OfType<ThrowActionDefinition>()
			    .FirstOrDefault();
	    }

	    if (throwActionDef == null)
	    {
		    GD.PrintErr($"ExplodeAction: ThrowActionDefinition not found on Item ({Item?.ItemData?.ItemName}) or Unit!");
		    return Task.CompletedTask;
	    }
	    
	    var results = Pathfinder.Instance.TryCalculateArcPath(startingGridCell, targetGridCell);
	    var path = results.GridCellPath as List<GridCell>;
	    var vectorPath = results.Vector3Path?.ToArray();

	    if (path == null || vectorPath == null || vectorPath.Length == 0)
	    {
		    GD.PrintErr("ExplodeAction: Failed to calculate arc path for throw.");
		    return Task.CompletedTask;
	    }
	    
	    var throwAction = new ThrowAction(
		    parentGridObject,
		    startingGridCell,
		    targetGridCell,
		    throwActionDef,
		    Item,
		    path,
		    vectorPath,
		    new  Godot.Collections.Dictionary<Enums.Stat, int>()
	    );

	    AddSubAction(throwAction);
    
	    GD.Print($"ExplodeAction: Successfully linked {throwActionDef.GetActionName()} sub-action.");
	    return Task.CompletedTask;
    }

    protected override async Task Execute()
    {
	    GD.Print($"ExplodeAction.Execute starting, SubActions were: {SubActions?.Count ?? 0}");
    
	    grenadeVisual = new CsgSphere3D 
	    { 
		    Radius = 0.3f,
		    Material = new StandardMaterial3D { AlbedoColor = Colors.Red }
	    };
	    parentGridObject.GetTree().Root.AddChild(grenadeVisual);
	    grenadeVisual.GlobalPosition = targetGridCell.WorldCenter;
	    
	    GD.Print($"Grenade armed. Explodes in {TurnsRemaining} turns at {targetGridCell.GridCoordinates}");
    
	    await Task.CompletedTask;
    }

    public bool OnTurnTick()
    {
        TurnsRemaining--;
        GD.Print($"Grenade ticking: {TurnsRemaining} turns remaining");
        
        if (grenadeVisual != null && grenadeVisual.Material is StandardMaterial3D mat)
        {
            mat.AlbedoColor = TurnsRemaining <= 1 ? Colors.Orange : Colors.Red;
        }
        
        return TurnsRemaining <= 0;
    }

    public void OnDelayComplete()
    {
        GD.Print($"BOOM! Grenade exploding at {targetGridCell.GridCoordinates}");
        
        var affectedCells = GetExplosionCells();
        foreach (var cell in affectedCells)
        {
            if (cell.HasGridObject())
            {
                var gridObjects = cell.gridObjects;

                for (int i = 0; i < gridObjects.Count; i++)
                {
	                GridObject gridObject = gridObjects[i];
	                if(!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out var gridObjectStatHolder)) continue;
	                
	                //TODO: make the grenade decide which stats it affects (i.e emp grnades, falshbangs affecting accuracy etc)

	                gridObjectStatHolder.TryRemoveStatCosts(costs);
                }
            }
        }
        
        grenadeVisual?.QueueFree();
    }

    protected override Task ActionComplete()
    {
        return Task.CompletedTask;
    }

    private List<GridCell> GetExplosionCells()
    {
        if (!GridSystem.Instance.TryGetGridCellsInRange(targetGridCell, new Vector2I(explosionRadius, explosionRadius),
            false,
            out List<GridCell> cells))
        {
            return new List<GridCell>();
        }
        return cells;
    }
}