using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot;

// Isolated runtime checks: no game autoloads, generated terrain or UI required.
public partial class ActionPerformanceRegression : Node3D
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GridSystem _grid;
    private GridObjectTeamHolder _team;
    private ActionManager _actions;
    private GridObject _unit;
    private GridObject _other;
    private Camera3D _camera;
    private int _uploads;

    public override async void _Ready()
    {
        try
        {
            SetupManagers();
            _unit = await CreateUnit(new Vector3I(12, 0, 12));
            _other = await CreateUnit(new Vector3I(30, 0, 30));
            _team.UpdateVisibility();
            await Frames(2);
            BenchmarkVisibility();
            if (!OS.GetCmdlineUserArgs().Contains("--benchmark-only"))
            {
                await CheckVisibility();
                await CheckLosInvalidation();
                await CheckActions();
                await CheckEnemyInterruption();
                await CheckFrameBudget();
            }
            GD.Print("PASS: action performance regression.");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
            GetTree().Quit(1);
        }
    }

    private static void Property(object obj, string name, object value) =>
        obj.GetType().GetProperty(name).SetValue(obj, value);
    private static void Field(object obj, string name, object value) =>
        obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void SetupManagers()
    {
        var game = new GameManager { mapSize = Vector2I.One };
        typeof(Manager<GameManager>).GetProperty("Instance").SetValue(null, game);
        var terrain = new MeshTerrainGenerator { chunkSize = 48, cellSize = Vector2.One };
        typeof(Manager<MeshTerrainGenerator>).GetProperty("Instance").SetValue(null, terrain);
        _grid = new GridSystem();
        AddChild(_grid);
        Field(_grid, "_cellSize", Vector2.One);
        var cells = new GridCell[20][,];
        for (int y = 0; y < cells.Length; y++)
        {
            cells[y] = new GridCell[48, 48];
            for (int x = 0; x < 48; x++)
                for (int z = 0; z < 48; z++)
                {
                    var coords = new Vector3I(x, y, z);
                    var center = new Vector3(x + .5f, y, z + .5f);
                    cells[y][x, z] = new GridCell(coords, center, center,
                        y == 0 ? Enums.GridCellState.Ground : Enums.GridCellState.Air,
                        Enums.FogState.Unseen, null);
                }
        }
        Property(_grid, "GridCells", cells);
        var objects = new GridObjectManager();
        AddChild(objects);
        _team = new GridObjectTeamHolder();
        Property(_team, "Team", Enums.UnitTeam.Player);
        objects.AddChild(_team);
        _team.Setup();
        Field(objects, "gridObjectTeams", new Godot.Collections.Dictionary<Enums.UnitTeam, GridObjectTeamHolder>
            { { Enums.UnitTeam.Player, _team } });
        _team.VisibilityChanged += (_, _) => _uploads++;
        _actions = new ActionManager();
        AddChild(_actions);
        var turns = new TurnManager();
        AddChild(turns);
        var turn = new Turn();
        Property(turn, "team", Enums.UnitTeam.Player);
        Field(turns, "turns", new[] { turn });
    }

    private async Task<GridObject> CreateUnit(Vector3I coords)
    {
        var unit = new GridObject { gridObjectSettings = Enums.GridObjectSettings.CanWalkThrough };
        var shape = new GridShape();
        shape.FillAll(true);
        unit.AddChild(new GridPositionData { Name = "GridPositionData", Shape = shape });
        unit.visualMesh = new Node3D();
        unit.AddChild(unit.visualMesh);
        var stats = new GridObjectStatHolder();
        foreach (var type in new[] { Enums.Stat.Health, Enums.Stat.TimeUnits, Enums.Stat.Stamina })
        {
            var stat = new GridObjectStat();
            Property(stat, "Stat", type);
            Property(stat, "CurrentValue", 100f);
            Field(stat, "maxValue", 100);
            stats.AddChild(stat);
        }
        unit.AddChild(stats);
        var sight = new GridObjectSight();
        unit.AddChild(sight);
        var actions = new GridObjectActions();
        Property(actions, "ActionDefinitions", new ActionDefinition[]
            { new MoveActionDefinition(), new MoveStepActionDefinition(), new RotateActionDefinition() });
        unit.AddChild(actions);
        AddChild(unit);
        var cell = _grid.GetGridCell(coords);
        unit.Position = cell.WorldCenter;
        await unit.Initialize(Enums.UnitTeam.Player, cell);
        await _team.AddGridObject(unit);
        return unit;
    }

    private void BenchmarkVisibility()
    {
        // Warm caches, then model the repeated root/child completion refreshes.
        _team.UpdateVisibility();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 24; i++) _team.UpdateVisibility();
        watch.Stop();
        GD.Print($"BENCH repeated visibility: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
            $"{GC.GetAllocatedBytesForCurrentThread() - allocated} managed bytes (24 refreshes, 48x48x20, two viewers).");
        allocated = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        for (int i = 0; i < 8; i++)
        {
            _unit.GridPositionData.SetDirection(RotationHelperFunctions.GetNextDirection(_unit.GridPositionData.Direction, true));
            _team.UpdateGridObjects(new RotateActionDefinition(), null);
        }
        watch.Stop();
        GD.Print($"BENCH full scan: {watch.Elapsed.TotalMilliseconds:F2} ms, " +
            $"{GC.GetAllocatedBytesForCurrentThread() - allocated} managed bytes (eight headings with completion refreshes).");
    }

    private async Task CheckVisibility()
    {
        await Frames(2);
        int uploads = _uploads;
        var explored = _team.ExploredCells.ToHashSet();
        _unit.TryGetGridObjectNode<GridObjectSight>(out var sight);
        _other.TryGetGridObjectNode<GridObjectSight>(out var otherSight);
        for (int i = 0; i < 4; i++)
        {
            _unit.GridPositionData.SetDirection(RotationHelperFunctions.GetNextDirection(_unit.GridPositionData.Direction, true));
            explored.UnionWith(sight.VisibleCells);
            Require(_team.TeamVisibleCells.SetEquals(sight.VisibleCells.Concat(otherSight.VisibleCells)),
                "Turning one viewer erased another viewer's sight.");
            CheckFog();
        }
        Require(explored.IsSubsetOf(_team.ExploredCells), "Intermediate headings were not explored.");
        Require(_uploads == uploads, "Instant turns uploaded fog before the frame boundary.");
        await Frames(2);
        Require(_uploads == uploads + 1, $"Instant turns did not combine into one fog upload ({uploads} -> {_uploads}, visible {_team.TeamVisibleCells.Count}, direction {_unit.GridPositionData.Direction}, holder {_unit.TeamHolder == _team}).");
        uploads = _uploads;
        _team.UpdateVisibility();
        _team.UpdateVisibility();
        await Frames(2);
        Require(_uploads == uploads, "Unchanged visibility uploaded another texture.");

        var anchor = _grid.GetGridCell(new Vector3I(31, 0, 30));
        _other.GlobalPosition = anchor.WorldCenter;
        _other.GridPositionData.SetGridCell(anchor);
        _team.UpdateVisibility();
        var cached = _team.TeamVisibleCells.ToHashSet();
        sight.CalculateSightArea();
        otherSight.CalculateSightArea();
        Require(cached.SetEquals(sight.VisibleCells.Concat(otherSight.VisibleCells)),
            "Movement outside the action system left stale cached sight.");
        CheckFog();
        GD.Print("PASS: combined team sight, immediate gameplay fog, exploration, movement invalidation and deferred uploads.");
    }

    private void CheckFog()
    {
        var images = (Godot.Collections.Dictionary<int, Image>)typeof(GridObjectTeamHolder)
            .GetField("_visibilityImages", Private).GetValue(_team);
        foreach (var cell in _grid.AllGridCells)
        {
            var expected = _team.TeamVisibleCells.Contains(cell) ? Enums.FogState.Visible :
                _team.ExploredCells.Contains(cell) ? Enums.FogState.PreviouslySeen : Enums.FogState.Unseen;
            Require(cell.fogState == expected, $"Incorrect fog at {cell.GridCoordinates}");
            float expectedPixel = expected == Enums.FogState.Visible ? 1f :
                expected == Enums.FogState.PreviouslySeen ? .5f : 0f;
            var coords = cell.GridCoordinates;
            Require(Mathf.Abs(images[coords.Y].GetPixel(coords.X, coords.Z).R - expectedPixel) < .005f,
                $"Incorrect fog pixel at {coords}");
        }
    }

    private async Task CheckLosInvalidation()
    {
        _unit.GridPositionData.SetDirection(Enums.Direction.East);
        var target = _grid.GetGridCell(new Vector3I(17, 0, 12));
        var blocker = new StaticBody3D { CollisionLayer = (uint)PhysicsLayer.LOS_BLOCKER };
        var collider = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.5f, 10, 10) } };
        blocker.AddChild(collider);
        AddChild(blocker);
        blocker.Position = _unit.GlobalPosition + new Vector3(2, 2, 0);
        await Frames(2);
        _grid.MarkNavigationChanged();
        _team.UpdateVisibility();
        Require(!_team.TeamVisibleCells.Contains(target), "Closed sight blocker did not hide the target.");
        collider.Disabled = true;
        await Frames(2);
        _grid.MarkNavigationChanged();
        _team.UpdateVisibility();
        Require(_team.TeamVisibleCells.Contains(target), "Opening a sight blocker did not invalidate cached rays.");
        blocker.QueueFree();
        GD.Print("PASS: sight blocker invalidation.");
    }

    private void FocusCamera()
    {
        _camera.GlobalPosition = _unit.GlobalPosition + new Vector3(0, 8, 12);
        _camera.LookAt(_unit.GlobalPosition);
    }

    private float TimeUnits()
    {
        _unit.TryGetGridObjectNode<GridObjectStatHolder>(out var stats);
        stats.TryGetStat(Enums.Stat.TimeUnits, out var stat);
        return stat.CurrentValue;
    }

    private async Task CheckActions()
    {
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 20, Current = true };
        AddChild(_camera);
        FocusCamera();
        var start = _unit.GridPositionData.AnchorCell;
        var target = _grid.GetGridCell(start.GridCoordinates + Vector3I.Right);
        var definition = new MoveStepActionDefinition();
        float initialCost = TimeUnits();
        var move = new MoveStepActionBase(_unit, start, target, definition, new() { { Enums.Stat.TimeUnits, 3 } });
        var run = move.ExecuteCall();
        await Frames(2);
        Require(!run.IsCompleted, "An on-screen move did not animate.");
        _camera.Position += Vector3.Right * 1000;
        await Frames(3);
        Require(run.IsCompletedSuccessfully, "Moving the camera away did not finish the active move promptly.");
        Require(_unit.GridPositionData.AnchorCell == target && _unit.GlobalPosition == target.WorldCenter,
            "Skipped move failed to commit its final position.");
        Require(TimeUnits() == initialCost - 3, "Skipped movement costs were lost or duplicated.");

        var rotation = new RotateActionBase(_unit, target, target, new RotateActionDefinition(), new(), Enums.Direction.West);
        await rotation.ExecuteCall();
        Require(_unit.GridPositionData.Direction == Enums.Direction.West, "Skipped rotation failed to commit heading.");
        Require(Mathf.Abs(_unit.visualMesh.Quaternion.Dot(new Quaternion(Vector3.Up,
            RotationHelperFunctions.GetRotationRadians(Enums.Direction.West)))) > .9999f,
            "Skipped rotation left a partial visual rotation.");

        _unit.GridPositionData.SetDirection(Enums.Direction.East);
        FocusCamera();
        var cancelTarget = _grid.GetGridCell(target.GridCoordinates + Vector3I.Right);
        var canceled = new MoveStepActionBase(_unit, target, cancelTarget, definition, new() { { Enums.Stat.TimeUnits, 3 } });
        var cancelRun = canceled.ExecuteCall();
        await Frames(2);
        await canceled.CancelCall();
        await Frames(3);
        Require(cancelRun.IsCompletedSuccessfully && _unit.GridPositionData.AnchorCell == target &&
            _unit.GlobalPosition == target.WorldCenter, "Canceling a partial step did not restore its position.");
        Require(TimeUnits() == initialCost - 3, "Canceled step charged its cost.");
        GD.Print("PASS: visible animation, camera-away completion, final positions/headings, costs and cancellation.");
    }

    private async Task CheckEnemyInterruption()
    {
        var start = _unit.GridPositionData.AnchorCell;
        var enemy = await CreateUnit(start.GridCoordinates + Vector3I.Right * 13);
        _team.GridObjects[Enums.GridObjectState.Active].Remove(enemy);
        Property(enemy, "Team", Enums.UnitTeam.Enemy);
        var enemies = new GridObjectTeamHolder();
        Property(enemies, "Team", Enums.UnitTeam.Enemy);
        GridObjectManager.Instance.AddChild(enemies);
        enemies.Setup();
        enemies.GridObjects[Enums.GridObjectState.Active].Add(enemy);
        Property(enemy, "TeamHolder", enemies);
        GridObjectManager.Instance.GetGridObjectTeamHolders()[Enums.UnitTeam.Enemy] = enemies;
        _team.UpdateVisibility();
        Require(!_team.TeamVisibleCells.Contains(enemy.GridPositionData.AnchorCell),
            "Enemy interruption fixture starts with the enemy visible.");
        _camera.Position += Vector3.Right * 1000;
        float initialCost = TimeUnits();
        var path = new RegressionPath(_unit, new[] { start,
            _grid.GetGridCell(start.GridCoordinates + Vector3I.Right),
            _grid.GetGridCell(start.GridCoordinates + Vector3I.Right * 2) });
        Property(_actions, "CurrentAction", path);
        _actions.SetIsBusy(true);
        await path.ExecuteCall();
        Require(path.WasInterruptedByNewEnemy && path.IsCancellationRequested,
            "Instant path failed to interrupt when it revealed an enemy.");
        Require(_unit.GridPositionData.AnchorCell.GridCoordinates == start.GridCoordinates + Vector3I.Right,
            "Enemy interruption rolled back the completed step or ran the next step.");
        Require(TimeUnits() == initialCost - 1, "Interrupted path charged incorrect completed-step costs.");
        GD.Print("PASS: skipped movement still stops at the first revealed enemy and charges only completed steps.");

        start = _unit.GridPositionData.AnchorCell;
        var northEnemy = await CreateUnit(start.GridCoordinates + Vector3I.Forward * 10);
        _team.GridObjects[Enums.GridObjectState.Active].Remove(northEnemy);
        Property(northEnemy, "Team", Enums.UnitTeam.Enemy);
        Property(northEnemy, "TeamHolder", enemies);
        enemies.GridObjects[Enums.GridObjectState.Active].Add(northEnemy);
        _team.UpdateVisibility();
        Require(!_team.TeamVisibleCells.Contains(northEnemy.GridPositionData.AnchorCell),
            "Rotation interruption fixture starts with the enemy visible.");
        var turnThenMove = new RegressionPath(_unit, new[] { start,
            _grid.GetGridCell(start.GridCoordinates + Vector3I.Forward) });
        Property(_actions, "CurrentAction", turnThenMove);
        _actions.SetIsBusy(true);
        await turnThenMove.ExecuteCall();
        Require(turnThenMove.WasInterruptedByNewEnemy && _unit.GridPositionData.AnchorCell == start,
            "A turn that revealed an enemy incorrectly continued into movement.");
        GD.Print("PASS: intermediate rotation sight still interrupts before the following movement step.");
    }

    private sealed class RegressionPath : ActionBase, ICompositeAction
    {
        private readonly GridCell[] _path;
        public ActionBase ParentActionBase { get; set; }
        public List<ActionBase> SubActions { get; set; } = new();
        public RegressionPath(GridObject unit, GridCell[] path)
            : base(unit, path[0], path[^1], new MoveActionDefinition(), new()) => _path = path;
        protected override Task Setup()
        {
            for (int i = 0; i < _path.Length - 1; i++)
                AddSubAction(new MoveStepActionBase(parentGridObject, _path[i], _path[i + 1],
                    new MoveStepActionDefinition(), new() { { Enums.Stat.TimeUnits, 1 } }));
            return Task.CompletedTask;
        }
        protected override Task Execute() => Task.CompletedTask;
        protected override Task ActionComplete() => Task.CompletedTask;
    }

    private async Task CheckFrameBudget()
    {
        ulong startFrame = Engine.GetProcessFrames();
        var yieldMethod = typeof(ActionManager).GetMethod("YieldIfActionBudgetExceeded");
        Require(yieldMethod != null, "Action frame budget is missing.");
        for (int i = 0; i < 32; i++)
        {
            await (Task)yieldMethod.Invoke(_actions, null);
            var work = Stopwatch.StartNew();
            while (work.Elapsed.TotalMilliseconds < 1) { }
        }
        ulong elapsedFrames = Engine.GetProcessFrames() - startFrame;
        Require(elapsedFrames > 1 && elapsedFrames < 32,
            $"Action work did not batch within a frame budget ({elapsedFrames} frames).");
        GD.Print($"PASS: 32 instant work items yielded across {elapsedFrames} frames.");
    }
}
