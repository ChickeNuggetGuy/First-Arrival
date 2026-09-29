using System.Linq;
using FirstArrival.Scripts.Utility;

public partial class GridObjectTeamHolder
{
    public void SetUnitActive(GridObject unit, bool active)
    {
        if (GridObjects == null) return;
        GridObjects[Enums.GridObjectState.Active].Remove(unit);
        GridObjects[Enums.GridObjectState.Inactive].Remove(unit);
        GridObjects[active ? Enums.GridObjectState.Active : Enums.GridObjectState.Inactive].Add(unit);
        var holder = active ? _activeUnitsHolder : _inactiveUnitsHolder;
        if (unit.GetParent() != holder) unit.Reparent(holder);
        unit.SetIsActive(active);
        if (!active && CurrentGridObject == unit)
            SetSelectedGridObject(GridObjects[Enums.GridObjectState.Active].FirstOrDefault());
        if (active && unit.TryGetGridObjectNode<GridObjectSight>(out var sight))
            sight.CalculateSightArea();
        UpdateVisibility();
        EmitSignal(SignalName.GridObjectListChanged, this);
    }
}
