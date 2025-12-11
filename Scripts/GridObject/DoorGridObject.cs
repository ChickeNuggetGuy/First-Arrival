using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class DoorGridObject : GridObject, IInteractableGridobject
{
    [Export] private Node3D pivot;
    [Export] private GridPositionData doorData;
    [Export] public Godot.Collections.Dictionary<Enums.Stat, int> costs { get; set; }

    public bool isOpen { get; protected set; } = false;

    private bool _initialized = false;
    private readonly List<GridCell> _doorCells = new List<GridCell>();

    public override async Task Initialize(Enums.UnitTeam team, GridCell g)
    {
        await base.Initialize(team, g);

        if (_initialized) return;

        GridSystem gridSystem = GridSystem.Instance;
        if (gridSystem == null)
        {
            GD.PrintErr("GridSystem is null");
            return;
        }

        _doorCells.Clear();
        var foundCells = gridSystem.GetCellsFromGridShape(doorData);
        if (foundCells != null) _doorCells.AddRange(foundCells);

        if (_doorCells.Count == 0)
        {
            GD.PrintErr($"Door {Name}: No cells found");
            return;
        }

        GD.Print($"\n=== Door Init: {_doorCells.Count} cells found ===");

        foreach (var doorCell in _doorCells)
        {
            var oldState = doorCell.state;
            var connectionsBeforeInit = gridSystem.GetConnections(doorCell.gridCoordinates);
            
            GD.Print($"Cell {doorCell.gridCoordinates}:");
            GD.Print($"  State before: {oldState}");
            GD.Print($"  Connections before: {connectionsBeforeInit.Count}");

            Enums.GridCellState newState = doorCell.state | Enums.GridCellState.Obstructed;
            doorCell.ModifyOriginalState(newState);
            doorCell.SetState(newState);

            var connectionsAfterInit = gridSystem.GetConnections(doorCell.gridCoordinates);
            
            GD.Print($"  State after: {doorCell.state}");
            GD.Print($"  Connections after: {connectionsAfterInit.Count}");
            
            if (connectionsAfterInit.Count > 0)
            {
                GD.PrintErr($"  WARNING: Door cell still has {connectionsAfterInit.Count} connections!");
                foreach (var conn in connectionsAfterInit)
                {
                    GD.PrintErr($"    Connected to: {conn}");
                }
            }
        }

        UpdateVisuals();
        _initialized = true;
        GD.Print("=== Door Init Complete ===\n");
    }

    public void Interact()
    {
        if (!_initialized) return;

        isOpen = !isOpen;
        UpdateVisuals();

        GD.Print($"\n=== Door {(isOpen ? "OPENING" : "CLOSING")} ===");

        foreach (var doorCell in _doorCells)
        {
            var oldState = doorCell.state;
            var oldConnections = GridSystem.Instance.GetConnections(doorCell.gridCoordinates);

            Enums.GridCellState newState;
            if (isOpen)
                newState = oldState & ~Enums.GridCellState.Obstructed;
            else
                newState = oldState | Enums.GridCellState.Obstructed;

            GD.Print($"Cell {doorCell.gridCoordinates}:");
            GD.Print($"  State: {oldState} -> {newState}");
            GD.Print($"  Connections before: {oldConnections.Count}");

            doorCell.ModifyOriginalState(newState);
            doorCell.SetState(newState);

            var newConnections = GridSystem.Instance.GetConnections(doorCell.gridCoordinates);
            GD.Print($"  Connections after: {newConnections.Count}");

            if (!isOpen && newConnections.Count > 0)
            {
                GD.PrintErr($"  ERROR: Closed door still has {newConnections.Count} connections!");
                foreach (var conn in newConnections)
                {
                    GD.PrintErr($"    Connected to: {conn}");
                }
            }
        }

        GD.Print($"=== Door {(isOpen ? "OPENED" : "CLOSED")} ===\n");
    }

    private void UpdateVisuals()
    {
        if (pivot != null)
        {
            pivot.Visible = !isOpen;
            var collider = pivot.GetNodeOrNull<CollisionObject3D>("StaticBody3D");
            if (collider != null)
            {
                collider.ProcessMode = isOpen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            }
        }
    }

    public override void _Process(double delta)
    {
	    base._Process(delta);
	    foreach (var doorCell in _doorCells)
	    {
		    DebugDraw3D.DrawBox(doorCell.worldCenter, Quaternion.Identity, Vector3.One, Colors.Red, true);
	    }
    }

    public bool IsDoorCell(GridCell cell)
    {
        return cell != null && _doorCells.Contains(cell);
    }

    public List<GridCell> GetInteractableCells()
    {
	    if (_doorCells.Count < 1) return new List<GridCell>();
	    
	    return _doorCells.Where(cell => cell.state.HasFlag(Enums.GridCellState.Ground)).ToList();
    }
}