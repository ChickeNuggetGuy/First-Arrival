using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

// Run in an isolated project without the game's autoload boot sequence.
public partial class DoorNavigationRegression : Node3D
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GridSystem grid;
    private Pathfinder pathfinder;

    public override async void _Ready()
    {
        try
        {
            var game = new GameManager { mapSize = Vector2I.One };
            typeof(Manager<GameManager>).GetProperty("Instance").SetValue(null, game);
            var terrain = new MeshTerrainGenerator { chunkSize = 24, cellSize = Vector2.One };
            typeof(Manager<MeshTerrainGenerator>).GetProperty("Instance").SetValue(null, terrain);
            grid = new GridSystem();
            AddChild(grid);
            SetField("_cellSize", Vector2.One);
            SetField("_cellConnections", new HashSet<CellConnection>());
            SetField("_adj", new Dictionary<Vector3I, HashSet<Vector3I>>());
            SetField("_corridorBox", new BoxShape3D());
            SetField("_corridorParams", new PhysicsShapeQueryParameters3D { CollideWithBodies = true, CollideWithAreas = false });
            pathfinder = new Pathfinder();
            AddChild(pathfinder);
            CheckOffsetColliderFootprints();
            foreach (float height in new[] { 0f, .5f, 1f, 1.5f })
                for (int turns = 0; turns < 4; turns++)
                    await CheckCraft(turns, height);
            await CheckObjectActionTargets();
            GD.Print("PASS: door navigation regression (16 craft placements, three open/close cycles each, six offset collider cases).");
            pathfinder.Free();
            grid.Free();
            terrain.Free();
            game.Free();
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
            GetTree().Quit(1);
        }
    }

    private void SetField(string name, object value) => typeof(GridSystem).GetField(name, PrivateInstance).SetValue(grid, value);

    private void ResetGrid(float height)
    {
        var cells = new GridCell[20][,];
        for (int y = 0; y < 20; y++)
        {
            cells[y] = new GridCell[24, 24];
            for (int x = 0; x < 24; x++)
                for (int z = 0; z < 24; z++)
                {
                    var coords = new Vector3I(x, y, z);
                    cells[y][x, z] = new GridCell(coords, new Vector3(x + .5f, y == Mathf.FloorToInt(height) ? height : y, z + .5f),
                        new Vector3(x + .5f, y + .5f, z + .5f),
                        y == Mathf.FloorToInt(height) ? Enums.GridCellState.Ground : Enums.GridCellState.Air,
                        Enums.FogState.Visible, null);
                }
        }
        typeof(GridSystem).GetProperty("GridCells").SetValue(grid, cells);
        SetField("_cellConnections", new HashSet<CellConnection>());
        SetField("_adj", new Dictionary<Vector3I, HashSet<Vector3I>>());
    }

    private async Task CheckCraft(int turns, float height)
    {
        ResetGrid(height);
        var craft = GD.Load<PackedScene>("res://Scenes/craft.tscn").Instantiate<Node3D>();
        typeof(MeshTerrainGenerator).GetMethod("RotateNestedManualGridShapes", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { craft, turns });
        AddChild(craft);
        craft.Rotation = new Vector3(0, turns * Mathf.Pi * .5f, 0);
        var anchor = craft.GetNode<GridPositionData>("Craft/CellStateOveride/GridPositionData");
        craft.Position += new Vector3(12.5f - anchor.GlobalPosition.X, height, 12.5f - anchor.GlobalPosition.Z);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (GridObject obj in GetTree().GetNodesInGroup("GridObjects").OfType<GridObject>())
            await obj.Initialize(obj.Team, null);
        foreach (GridCellStateOverride obj in GetTree().GetNodesInGroup("GridCellOverride").OfType<GridCellStateOverride>())
            obj.InitializeGridCellOverride();
        typeof(GridSystem).GetMethod("BuildAllConnections", PrivateInstance).Invoke(grid, null);
        var door = craft.GetNode<DoorGridObject>("Craft/DoorGridObject");
        var data = door.GetNode<GridPositionData>("GridObjectNodes/DoorPositionData");
        var inside = grid.GetGridCell(GridConfiguration.GetActive().WorldToGrid(craft.ToGlobal(new Vector3(3, .1f, 4))));
        var outside = grid.GetGridCell(GridConfiguration.GetActive().WorldToGrid(craft.ToGlobal(new Vector3(3, .1f, 7))));
        string placement = $"rotation {turns * 90}, height {height}";
        var doorCells = grid.GetCellsFromGridShape(data);
        Require(doorCells.Count == 6, $"Door footprint incomplete: {placement}");
        var fixedFrameCells = door.GridPositionData.OccupiedCells
            .Where(c => !doorCells.Contains(c) && c.state.HasFlag(Enums.GridCellState.Obstructed)).ToList();

        // Match the standard unit's 1 x 2 x 1 footprint, exercising the actual
        // shaped pathfinder without loading unrelated unit inventory/AI systems.
        var unit = await CreateActionUnit(inside, Enums.UnitTeam.Player);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Require(!CanReach(inside, outside), $"Closed door is traversable: {placement}");
            Require(doorCells.All(c => c.Connections.Count == 0), $"Closed door retains connections: {placement}");
            Require(pathfinder.FindPathForGridObject(unit, inside, outside).Count == 0,
                $"Unit can path through a closed door: {placement}");
            CheckDoorTargets(unit, door, doorCells, placement);
            door.Interact();
            CheckDoorTargets(unit, door, doorCells, placement);
            Require(CanReach(inside, outside) && CanReach(outside, inside), $"Open door disconnects craft: {placement}");
            var path = pathfinder.FindPathForGridObject(unit, inside, outside);
            Require(path.Count > 0 && path.Any(door.IsDoorCell), $"Unit cannot path through open doorway: {placement}");
            Require(door.GetInteractableCells().All(c => c.IsWalkable), $"Open door cell remains blocked: {placement}");
            Require(fixedFrameCells.All(c => c.state.HasFlag(Enums.GridCellState.Obstructed)),
                $"Opening door cleared its fixed frame: {placement}");
            door.Interact();
        }
        Require(!CanReach(inside, outside), $"Reclosing failed: {placement}");
        GD.Print($"PASS: {placement}");
        unit.GridPositionData.SetGridCell(null);
        unit.Free();
        craft.Free();
    }

    private void CheckOffsetColliderFootprints()
    {
        ResetGrid(0);
        var config = GridConfiguration.GetActive();
        var anchor = new Vector3I(12, 5, 12);
        foreach (var offset in new[] { Vector3I.Right, Vector3I.Left, Vector3I.Up, Vector3I.Down, Vector3I.Forward, Vector3I.Back })
        {
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .5f } });
            AddChild(body);
            body.Position = config.GridToWorld(anchor + offset * 3);
            var shape = GridShape.CreateFromCollisionBounds(body, config, config.GridToWorld(anchor));
            var occupied = shape.GetWorldCoordinates(anchor);
            Require(occupied.Count == 1 && occupied[0] == anchor + offset * 3,
                $"Offset collider was shifted or gained a phantom anchor cell: {offset}");
            body.Free();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private bool CanReach(GridCell start, GridCell end)
    {
        var seen = new HashSet<Vector3I> { start.GridCoordinates };
        var queue = new Queue<Vector3I>();
        queue.Enqueue(start.GridCoordinates);
        while (queue.TryDequeue(out var current))
        {
            if (current == end.GridCoordinates) return true;
            foreach (var next in grid.GetConnections(current))
                if (seen.Add(next)) queue.Enqueue(next);
        }
        return false;
    }
}
