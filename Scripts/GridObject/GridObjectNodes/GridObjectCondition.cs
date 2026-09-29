using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

/// <summary>Owns life state; the body is only a movable representation of this unit.</summary>
[GlobalClass]
public partial class GridObjectCondition : GridObjectNode
{
    public Enums.UnitCondition State { get; private set; }
    public UnitBodyItem Body { get; private set; }
    public GridObjectStat Health { get; private set; }
    public GridObjectStat Stun { get; private set; }
    private GridObjectStatHolder _stats;
    private bool _changing;
    private bool _stimulantRecovery;
    private readonly Dictionary<CollisionObject3D, (uint layer, uint mask)> _collisions = new();

    [Signal] public delegate void ConditionChangedEventHandler(GridObject unit, int condition);

    protected override void Setup()
    {
        if (Health != null) Health.CurrentValueChanged -= OnStatChanged;
        if (Stun != null) Stun.CurrentValueChanged -= OnStatChanged;
        if (!parentGridObject.TryGetGridObjectNode(out _stats)) return;
        _stats.TryGetStat(Enums.Stat.Health, out var health);
        _stats.TryGetStat(Enums.Stat.Stun, out var stun);
        Health = health;
        Stun = stun;
        if (Health != null) Health.CurrentValueChanged += OnStatChanged;
        if (Stun != null) Stun.CurrentValueChanged += OnStatChanged;
    }

    private void OnStatChanged(int value, GridObject unit) => Evaluate();

    public void ApplyStun(float amount)
    {
        if (State == Enums.UnitCondition.Dead || amount <= 0) return;
        if (Stun != null && Stun.CurrentValue + amount >= Health.CurrentValue)
            _stimulantRecovery = false;
        Stun?.AddValue(amount);
    }

    public bool ApplyStimulant(float amount = 4)
    {
        if (State == Enums.UnitCondition.Dead || Stun == null || amount <= 0) return false;
        if (State == Enums.UnitCondition.Unconscious && Stun.CurrentValue - amount < Health.CurrentValue)
            _stimulantRecovery = true;
        Stun.RemoveValue(amount);
        Evaluate();
        return State == Enums.UnitCondition.Conscious;
    }

    public void Evaluate()
    {
        if (_changing || parentGridObject.IsRestoring || Health == null || Stun == null ||
            State == Enums.UnitCondition.Dead) return;
        if (Health.CurrentValue <= 0)
        {
            Collapse(Enums.UnitCondition.Dead);
        }
        else if (Stun.CurrentValue >= Health.CurrentValue)
        {
            if (State == Enums.UnitCondition.Conscious && parentGridObject.IsActive)
                Collapse(Enums.UnitCondition.Unconscious);
        }
        else if (State == Enums.UnitCondition.Unconscious)
        {
            TryWake();
        }
        Body?.RefreshIdentity();
    }

    private void Collapse(Enums.UnitCondition state)
    {
        _changing = true;
        try
        {
            GridCell cell = parentGridObject.GridPositionData?.AnchorCell;
            State = state;
            if (Body == null && cell?.InventoryGrid != null)
            {
                Body = UnitBodyItem.Create(parentGridObject);
                AttachBody(Body);
                if (!cell.InventoryGrid.TryAddItem(Body, 1))
                    GD.PushError($"Could not place body for {parentGridObject.Name}.");
            }
            if (cell?.InventoryGrid != null &&
                parentGridObject.TryGetGridObjectNode<GridObjectInventory>(out var inventory))
            {
                foreach (InventoryGrid source in inventory.InventoryGrids.Values)
                    foreach (var entry in source.UniqueItems.ToArray())
                        InventoryGrid.TryTransferItem(source, cell.InventoryGrid, entry.item, entry.count);
            }
            SetTimeUnits(0);
            parentGridObject.TeamHolder?.SetUnitActive(parentGridObject, false);
            parentGridObject.SetIsActive(false);
            RestorePresentation();
            Body?.RefreshIdentity();
            EmitSignal(SignalName.ConditionChanged, parentGridObject, (int)State);
        }
        finally { _changing = false; }
    }

    public bool TryWake()
    {
        if (State != Enums.UnitCondition.Unconscious || Health.CurrentValue <= 0 ||
            Stun.CurrentValue >= Health.CurrentValue || Body?.currentGrid == null) return false;
        GridCell origin = Body.GetWorldCell();
        if (origin == null) return false;
        // Own square first, then north and clockwise. CanOccupyAt checks all cells
        // and support for large units as well as other units and static terrain.
        Vector3I[] offsets = { Vector3I.Zero, new(0, 0, -1), new(1, 0, -1),
            new(1, 0, 0), new(1, 0, 1), new(0, 0, 1), new(-1, 0, 1),
            new(-1, 0, 0), new(-1, 0, -1) };
        foreach (Vector3I offset in offsets)
        {
            var cell = GridSystem.Instance?.GetGridCell(origin.GridCoordinates + offset);
            if (cell == null || !parentGridObject.GridPositionData.CanOccupyAt(
                    cell, parentGridObject.GridPositionData.Direction)) continue;
            _changing = true;
            try
            {
                UnitBodyItem body = Body;
                if (!body.currentGrid.TryRemoveItem(body, 1)) return false;
                Body = null;
                body.Unlink();
                body.QueueFree();
                State = Enums.UnitCondition.Conscious;
                parentGridObject.GlobalPosition = cell.WorldCenter;
                parentGridObject.SetIsActive(true);
                parentGridObject.GridPositionData.SetGridCell(cell);
                RestorePresentation();
                SetTimeUnits(_stimulantRecovery ? 0 : _stats.GetEffectiveMaxValue(Enums.Stat.TimeUnits));
                _stimulantRecovery = false;
                parentGridObject.TeamHolder?.SetUnitActive(parentGridObject, true);
                EmitSignal(SignalName.ConditionChanged, parentGridObject, (int)State);
                return true;
            }
            finally { _changing = false; }
        }
        return false; // Keep the body and retry on a subsequent turn.
    }

    public void ProcessTurn()
    {
        if (State == Enums.UnitCondition.Dead) return;
        // Bleeding precedes recovery: a critically injured unconscious unit can die.
        Health.ApplyFatalWoundBleeding();
        if (State == Enums.UnitCondition.Dead) return;
        Stun.RemoveValue(1);
        Evaluate(); // Also retry when stun is already zero but space was blocked.
        if (State != Enums.UnitCondition.Conscious) SetTimeUnits(0);
    }

    public void DestroyBody()
    {
        if (State == Enums.UnitCondition.Conscious) return;
        Health.SetValue(0);
        Evaluate();
        if (Body == null) return;
        var body = Body;
        Body = null;
        body.currentGrid?.TryRemoveItem(body, 1);
        body.Unlink();
        body.QueueFree();
    }

    public void AttachBody(UnitBodyItem body)
    {
        Body = body;
        body.Link(parentGridObject);
        if (body.GetParent() == null)
            (parentGridObject.TeamHolder as Node ?? parentGridObject.GetParent()).AddChild(body);
        body.RefreshIdentity();
    }

    public void RestorePresentation()
    {
        // Old saves had only active/inactive state. Infer death without spawning
        // a new body at an arbitrary position for those historical casualties.
        if (Health?.CurrentValue <= 0) State = Enums.UnitCondition.Dead;
        bool down = State != Enums.UnitCondition.Conscious;
        if (down)
        {
            parentGridObject.SetIsActive(false);
            CaptureCollisions(parentGridObject);
            foreach (var collision in _collisions.Keys)
            {
                collision.CollisionLayer = 0;
                collision.CollisionMask = 0;
            }
            parentGridObject.Hide();
        }
        else
        {
            foreach (var pair in _collisions)
            {
                if (!GodotObject.IsInstanceValid(pair.Key)) continue;
                pair.Key.CollisionLayer = pair.Value.layer;
                pair.Key.CollisionMask = pair.Value.mask;
            }
            _collisions.Clear();
            if (parentGridObject.IsActive) parentGridObject.Show();
        }
    }

    private void CaptureCollisions(Node node)
    {
        if (node is CollisionObject3D collision && !_collisions.ContainsKey(collision))
            _collisions[collision] = (collision.CollisionLayer, collision.CollisionMask);
        foreach (Node child in node.GetChildren()) CaptureCollisions(child);
    }

    private void SetTimeUnits(float value)
    {
        if (_stats.TryGetStat(Enums.Stat.TimeUnits, out var timeUnits)) timeUnits.SetValue(value);
    }

    public override Godot.Collections.Dictionary<string, Variant> Save() => new()
    {
        ["state"] = (int)State,
        ["stimulantRecovery"] = _stimulantRecovery
    };

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        State = (Enums.UnitCondition)data["state"].AsInt32();
        _stimulantRecovery = data.TryGetValue("stimulantRecovery", out var value) && value.AsBool();
    }
}
