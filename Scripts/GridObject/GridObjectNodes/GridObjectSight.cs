using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using Godot;

[GlobalClass]
public partial class GridObjectSight : GridObjectNode
{
  [Export] private int sightRange = 10;
  [Export(PropertyHint.Range, "0,180")] private float fieldOfView = 90;

  private readonly List<GridObject> _seenGridObjects = new();
  public IReadOnlyList<GridObject> SeenGridObjects => _seenGridObjects.AsReadOnly();
  
  private readonly List<GridObject> _previouslySeenGridObjects = new();
  public IReadOnlyList<GridObject> PreviouslySeenGridObjects => _previouslySeenGridObjects.AsReadOnly();

  private readonly List<GridCell> _visibleCells = new();
  public IReadOnlyList<GridCell> VisibleCells => _visibleCells.AsReadOnly();

  protected override void Setup()
  {
    CalculateSightArea();
  }
  
  public void ClearSightOnNewTurn()
  {
      _seenGridObjects.Clear();
      _previouslySeenGridObjects.Clear();
  }

  public void CalculateSightArea()
  {
    if (parentGridObject == null || !parentGridObject.IsInitialized)
      return;

    var newlySeenObjects = new List<GridObject>();
    var currentVisibleCells = new List<GridCell>();

    GridSystem gridSystem = GridSystem.Instance;
    if (gridSystem == null)
    {
      GD.PrintErr("GridSystem instance not found.");
      return;
    }

    GridCell startCell = parentGridObject.GridPositionData.GridCell;
    if (startCell == GridCell.Null)
      return;

    currentVisibleCells.Add(startCell);

    if (
      !gridSystem.TryGetGridCellsInRange(
        startCell,
        new Vector2I(sightRange, sightRange),
        false,
        out List<GridCell> cellsInRadius
      )
    )
      return;

    if (
      !gridSystem.TryGetGridCellsInRange(
        startCell,
        new Vector2I(3, 3),
        false,
        out List<GridCell> immediateCells
      )
    )
      return;

    currentVisibleCells.AddRange(immediateCells);
    
    Vector3 forwardVector = (-parentGridObject.Basis.Z).Normalized();

    foreach (var cell in cellsInRadius)
    {
      if (cell == startCell)
        continue;

      Vector3 directionToTarget = (cell.worldCenter - startCell.worldCenter).Normalized();

      float angle = Mathf.RadToDeg(
        forwardVector.AngleTo(directionToTarget)
      );

      if (Mathf.Abs(angle) <= fieldOfView / 2.0f)
      {
        // TODO: Add LOS checks (raycast) to handle obstacles.
        currentVisibleCells.Add(cell);
        if (cell.HasGridObject())
          newlySeenObjects.AddRange(cell.gridObjects);
      }
    }
    
    _visibleCells.Clear();
    _visibleCells.AddRange(currentVisibleCells.Distinct());

    var noLongerSeen = _seenGridObjects.Except(newlySeenObjects).ToList();
    foreach (var obj in noLongerSeen)
    {
        if (!_previouslySeenGridObjects.Contains(obj))
        {
            _previouslySeenGridObjects.Add(obj);
        }
    }

    _seenGridObjects.Clear();
    _seenGridObjects.AddRange(newlySeenObjects.Distinct());

    foreach (var obj in _seenGridObjects)
    {
        _previouslySeenGridObjects.Remove(obj);
    }
  }
}