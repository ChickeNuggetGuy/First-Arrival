using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using Godot;

[GlobalClass]
public partial class GridObjectSight : GridObjectNode
{
    [ExportCategory("Main Vision (Focused)")]
    [Export] private float _mainSightRange = 12.0f;
    [Export] private Vector2 _mainSightAngles = new Vector2(45f, 60f);

    [ExportCategory("Peripheral Vision (Wide)")]
    [Export] private float _peripheralSightRange = 5.0f;
    [Export] private Vector2 _peripheralSightAngles = new Vector2(90f, 60f);

    [ExportCategory("Proximity")]
    [Export] private bool _useProximityCheck = true;
    [Export] private int _proximityRadius = 3;

    [ExportCategory("Line Of Sight")]
    [Export] private bool _useLosForGridObjects = true;
	
    [Export] private bool _useLosForCells = false;


    [Export(PropertyHint.Layers3DPhysics)]
    private uint _losBlockerMask = 0;

    [Export] private float _eyeHeight = 1.6f;
    [Export] private float _targetHeight = 1.0f;

    private readonly List<GridObject> _seenGridObjects = new();
    public IReadOnlyList<GridObject> SeenGridObjects => _seenGridObjects.AsReadOnly();

    private readonly List<GridObject> _previouslySeenGridObjects = new();
    public IReadOnlyList<GridObject> PreviouslySeenGridObjects =>
        _previouslySeenGridObjects.AsReadOnly();

    private readonly List<GridCell> _visibleCells = new();
    public IReadOnlyList<GridCell> VisibleCells => _visibleCells.AsReadOnly();

    private readonly HashSet<GridCell> _tempCellSet = new();

    
    public bool HasCalculated { get; private set; }
    public bool IsDirty { get; private set; } = true;

    public void MarkDirty() => IsDirty = true;

    public void EnsureUpToDate()
    {
	    if (!HasCalculated || IsDirty)
	    {
		    CalculateSightArea();
	    }
    }
    
    
    protected override void Setup()
    {
        CalculateSightArea();
    }

    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
	    return new Godot.Collections.Dictionary<string, Variant>();
    }

    public override void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
	    return;
    }

    public void CalculateSightArea()
    {
        if (parentGridObject == null || !parentGridObject.IsInitialized)
            return;

        var gridSystem = GridSystem.Instance;
        if (gridSystem == null)
            return;

        var startCell = parentGridObject.GridPositionData.AnchorCell;
        if (startCell == null || startCell == GridCell.Null)
            return;

        _tempCellSet.Clear();
        _tempCellSet.Add(startCell);

        var newlySeenObjects = new List<GridObject>();

        Vector3 forwardVector = (-parentGridObject.GlobalBasis.Z).Normalized();

        var mainConeCells = gridSystem.GetGridCellsInCone(
            startCell,
            forwardVector,
            _mainSightRange,
            _mainSightAngles,
            includeOrigin: true
        );

        var peripheralConeCells = gridSystem.GetGridCellsInCone(
            startCell,
            forwardVector,
            _peripheralSightRange,
            _peripheralSightAngles,
            includeOrigin: true
        );

        foreach (var cell in mainConeCells)
            if (cell != null && cell != GridCell.Null)
                _tempCellSet.Add(cell);

        foreach (var cell in peripheralConeCells)
            if (cell != null && cell != GridCell.Null)
                _tempCellSet.Add(cell);

        if (_useProximityCheck)
        {
            if (gridSystem.TryGetGridCellsInRange(
                    startCell,
                    new Vector2I(_proximityRadius, _proximityRadius),
                    false,
                    out List<GridCell> immediateCells))
            {
                foreach (var cell in immediateCells)
                    if (cell != null && cell != GridCell.Null)
                        _tempCellSet.Add(cell);
            }
        }

        _visibleCells.Clear();

        // Viewer ray origin (eye)
        Vector3 eye = GetViewerEyePosition();

        foreach (var cell in _tempCellSet)
        {
            if (cell == null || cell == GridCell.Null)
                continue;

            // Optional: require LOS for the cell itself (affects fog/cell visibility)
            if (_useLosForCells && cell != startCell)
            {
                Vector3 cellPoint = cell.WorldCenter + Vector3.Up * _targetHeight;
                if (IsLineBlocked(eye, cellPoint))
                    continue;
            }

            _visibleCells.Add(cell);

            if (!cell.HasGridObject())
                continue;

            // LOS check for objects in the cell
            foreach (var obj in cell.gridObjects)
            {
                if (obj == null)
                    continue;
                
                if (obj == parentGridObject)
                {
                    newlySeenObjects.Add(obj);
                    continue;
                }

                if (!_useLosForGridObjects)
                {
                    newlySeenObjects.Add(obj);
                    continue;
                }

                if (IsGridObjectVisibleByLos(eye, obj))
                    newlySeenObjects.Add(obj);
            }
        }

        UpdateSeenLists(newlySeenObjects);
        
        HasCalculated = true;
        IsDirty = false;
    }

    private Vector3 GetViewerEyePosition()
    {
        Vector3 basePos = parentGridObject.objectCenter != null
            ? parentGridObject.objectCenter.GlobalPosition
            : parentGridObject.GlobalPosition;

        return basePos + Vector3.Up * _eyeHeight;
    }

    private Vector3 GetTargetAimPosition(GridObject target)
    {
        Vector3 basePos = target.objectCenter != null
            ? target.objectCenter.GlobalPosition
            : target.GlobalPosition;

        return basePos + Vector3.Up * _targetHeight;
    }

    private bool IsGridObjectVisibleByLos(Vector3 eye, GridObject target)
    {
        if (target == null || !target.IsInsideTree())
            return false;

        Vector3 aim = GetTargetAimPosition(target);
        return !IsLineBlocked(eye, aim);
    }

    private bool IsLineBlocked(Vector3 from, Vector3 to)
    {
        var world = GetTree().Root.GetWorld3D();
        if (world == null)
            return false;

        var space = world.DirectSpaceState;

        uint mask = _losBlockerMask;
        if (mask == 0)
        {
            // Fallback: if you have a PhysicsLayer enum, set a sensible default here.
            // Example (adjust to your project):
            // mask = (uint)PhysicsLayer.OBSTACLE | (uint)PhysicsLayer.TERRAIN;
            mask = uint.MaxValue;
        }

        var rayParams = new PhysicsRayQueryParameters3D
        {
            From = from,
            To = to,
            CollisionMask = mask,
            CollideWithBodies = true,
            CollideWithAreas = false
        };
        
        if (parentGridObject.collisionShape != null)
        {
            rayParams.Exclude = new Godot.Collections.Array<Rid>
            {
                parentGridObject.collisionShape.GetRid()
            };
        }

        var hit = space.IntersectRay(rayParams);
        return hit.Count > 0;
    }

    private void UpdateSeenLists(List<GridObject> newlySeenObjects)
    {
        var noLongerSeen = _seenGridObjects.Except(newlySeenObjects).ToList();

        foreach (var obj in noLongerSeen)
        {
            if (!_previouslySeenGridObjects.Contains(obj))
                _previouslySeenGridObjects.Add(obj);
        }

        _seenGridObjects.Clear();
        _seenGridObjects.Add(parentGridObject);
        _seenGridObjects.AddRange(newlySeenObjects.Distinct());

        foreach (var obj in _seenGridObjects)
            _previouslySeenGridObjects.Remove(obj);
    }
}