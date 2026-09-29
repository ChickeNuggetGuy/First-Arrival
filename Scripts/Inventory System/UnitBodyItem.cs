using System.Collections.Generic;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Inventory_System;

/// <summary>Unique inventory proxy. Never owns or copies the unit's components.</summary>
public partial class UnitBodyItem : Item
{
    public string UnitId { get; private set; }
    public GridObject LinkedUnit { get; private set; }
    public string UnitName { get; private set; }
    public bool IsDead { get; private set; }
    private InventoryGrid _lastInventory;
    private InventoryGrid _dragOrigin;

    public static UnitBodyItem Create(GridObject unit) => CreateSaved(
        unit.UnitId, unit.Name, unit.Condition.State == Enums.UnitCondition.Dead);

    public static UnitBodyItem CreateSaved(string unitId, string unitName, bool dead)
    {
        var body = new UnitBodyItem { UnitId = unitId, UnitName = unitName, IsDead = dead };
        body.Init(ItemData.CreateBodyData(unitName, dead));
        body.Visible = false;
        return body;
    }

    public void Link(GridObject unit) { LinkedUnit = unit; }
    public void Unlink() { LinkedUnit = null; }

    public string GetStatusText()
    {
        var condition = LinkedUnit?.Condition;
        return condition == null ? ItemData.ItemName :
            $"{ItemData.ItemName}\nHealth: {condition.Health.CurrentValue:0.#}" +
            $"\nStun: {condition.Stun.CurrentValue:0.#}\nFatal wounds: {condition.Health.GetTotalFatalWounds()}";
    }

    public void RefreshIdentity()
    {
        if (LinkedUnit == null) return;
        bool dead = LinkedUnit.Condition.State == Enums.UnitCondition.Dead;
        if (UnitName != LinkedUnit.Name.ToString() || IsDead != dead)
        {
            UnitName = LinkedUnit.Name;
            IsDead = dead;
            Init(ItemData.CreateBodyData(UnitName, dead));
        }
        currentGrid?.NotifyItemChanged();
    }

    // Transfers are synchronous. The cursor retains a body's physical origin.
    public void OnInventoryChanging(InventoryGrid previous, InventoryGrid next)
    {
        if (previous != null && previous.InventoryType != Enums.InventoryType.MouseHeld)
            _lastInventory = previous;
        if (next?.InventoryType == Enums.InventoryType.MouseHeld)
            _dragOrigin = _lastInventory;
        else if (next != null)
            _dragOrigin = null;
    }

    public GridCell GetWorldCell() => GetWorldCell(new HashSet<string>());

    private GridCell GetWorldCell(HashSet<string> visited)
    {
        if (!visited.Add(UnitId)) return null;
        InventoryGrid inventory = currentGrid?.InventoryType == Enums.InventoryType.MouseHeld
            ? _dragOrigin : currentGrid;
        if (inventory?.GroundCell != null) return inventory.GroundCell;
        GridObject carrier = inventory?.OwningUnit;
        return carrier?.GridPositionData?.AnchorCell ?? carrier?.Condition?.Body?.GetWorldCell(visited);
    }

    public override void _Process(double delta)
    {
        GridCell floor = currentGrid?.GroundCell;
        Visible = floor != null && floor.fogState == Enums.FogState.Visible;
        if (floor != null) GlobalPosition = floor.WorldCenter + Vector3.Up * 0.22f;
    }

    // Keep one world representation under the team holder. Equipment callbacks
    // still run, but cannot reparent the original unit or leave a ghost in a hand.
    public override void ShowVisual(BoneAttachment3D attachment) { }
    public override void HideVisual(BoneAttachment3D attachment) { }
}
