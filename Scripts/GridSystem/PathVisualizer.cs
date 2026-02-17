using Godot;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.UI;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class PathVisualizer : Node3D
{
    [Export] private PackedScene gridPathVisualScene;

    /// <summary>
    /// Maximum arrows to pre load.
    /// </summary>
    [Export] private int poolSize = 64;

    private readonly List<GridPathVisual> pool = new();
    private int activeCount;
    private GridCell lastHoveredCell;
    private bool lastWasVisible;

    public override void _Ready()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var instance = gridPathVisualScene.Instantiate<GridPathVisual>();
            AddChild(instance);
            instance.Hide();
            pool.Add(instance);
        }
    }

    public override void _Process(double delta)
    {
        if (!ShouldVisualize())
        {
            if (lastWasVisible)
            {
                ClearVisuals();
                lastWasVisible = false;
                lastHoveredCell = null;
            }
            return;
        }

        GridCell hoveredCell = InputManager.Instance.currentGridCell;

        if (hoveredCell == null || hoveredCell == GridCell.Null)
        {
            if (lastWasVisible) ClearVisuals();
            lastWasVisible = false;
            lastHoveredCell = null;
            return;
        }

        if (hoveredCell == lastHoveredCell)
            return;

        lastHoveredCell = hoveredCell;
        UpdatePathVisuals(hoveredCell);
    }


    private bool ShouldVisualize()
    {
        if (TurnManager.Instance?.CurrentTurn?.team != Enums.UnitTeam.Player)
            return false;

        if (ActionManager.Instance.IsBusy)
            return false;

        if (ActionManager.Instance.SelectedAction is not MoveActionDefinition)
            return false;

        var holder = GridObjectManager.Instance
            .GetGridObjectTeamHolder(Enums.UnitTeam.Player);
        if (holder?.CurrentGridObject == null)
            return false;

        if (InputManager.Instance.MouseOverUI)
            return false;

        return true;
    }
	

    private void UpdatePathVisuals(GridCell targetCell)
    {
        ClearVisuals();

        GridObject selectedUnit = GridObjectManager.Instance
            .GetGridObjectTeamHolder(Enums.UnitTeam.Player)
            .CurrentGridObject;

        GridCell startCell = selectedUnit.GridPositionData.AnchorCell;
        if (startCell == null) return;
        
        if (startCell == targetCell) return;

        // Calculate the path
        List<GridCell> path = Pathfinder.Instance.FindPath(startCell, targetCell);
        if (path == null || path.Count <= 1) return;
        
        var moveAction = ActionManager.Instance.SelectedAction
            as MoveActionDefinition;


        int currentTU = GetCurrentTimeUnits(selectedUnit);
        int currentStamina = GetCurrentStamina(selectedUnit);
        int tuCostPerStep = GetTUCostPerStep(moveAction);
        int staminaCostPerStep = GetStaminaCostPerStep(moveAction);

        int runningTU = currentTU;
        int runningStamina = currentStamina;

        // Skip index 0 — that's the cell the unit is already on
        for (int i = 1; i < path.Count; i++)
        {
            if (i - 1 >= pool.Count) break; // pool exhausted

            runningTU -= tuCostPerStep;
            runningStamina -= staminaCostPerStep;

            bool isReachable = runningTU >= 0 && runningStamina >= 0;

            // Direction: point toward the NEXT cell, or stay
            // facing forward on the last cell
            Vector3? lookTarget = i < path.Count - 1
                ? (Vector3?)path[i + 1].WorldCenter
                : null;

            pool[activeCount].Setup(
                path[i].WorldCenter,
                lookTarget,
                Mathf.Max(runningTU, 0),
                Mathf.Max(runningStamina, 0),
                isReachable
            );

            activeCount++;
        }

        lastWasVisible = activeCount > 0;
    }

    private void ClearVisuals()
    {
        for (int i = 0; i < activeCount; i++)
        {
            pool[i].Hide();
        }
        activeCount = 0;
    }
	

    private int GetCurrentTimeUnits(GridObject unit)
    {
	    if(!unit.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return -1;
	    
	    if(!statHolder.TryGetStat(Enums.Stat.TimeUnits, out GridObjectStat timeUnits)) return -1;

	    return (int)timeUnits.CurrentValue;
    }

    private int GetCurrentStamina(GridObject unit)
    {
	    if(!unit.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return -1;
	    
	    if(!statHolder.TryGetStat(Enums.Stat.TimeUnits, out GridObjectStat stamina)) return -1;

	    return (int)stamina.CurrentValue;
    }

    private int GetTUCostPerStep(MoveActionDefinition moveAction)
    {
        // TODO: return TU cost per cell from the action definition
        // e.g. return moveAction.timeUnitCost;
        return 4;
    }

    private int GetStaminaCostPerStep(MoveActionDefinition moveAction)
    {
        // TODO: return stamina cost per cell from the action definition
        // e.g. return moveAction.staminaCost;
        return 2;
    }
}