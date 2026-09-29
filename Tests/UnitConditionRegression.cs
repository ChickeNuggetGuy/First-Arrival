using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class UnitConditionRegression : Node3D
{
    private GridSystem _grid;
    private GridObjectManager _manager;
    private GridObjectTeamHolder _player;
    private GridObjectTeamHolder _enemy;
    private InventoryManager _inventory;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            var game = new GameManager { mapSize = Vector2I.One };
            typeof(Manager<GameManager>).GetProperty("Instance").SetValue(null, game);
            var terrain = new MeshTerrainGenerator { chunkSize = 12, cellSize = Vector2.One };
            typeof(Manager<MeshTerrainGenerator>).GetProperty("Instance").SetValue(null, terrain);
            _inventory = new InventoryManager();
            AddChild(_inventory);
            await _inventory.SetupCall(false);
            _grid = new GridSystem();
            AddChild(_grid);
            typeof(GridSystem).GetField("_cellSize", Fields).SetValue(_grid, Vector2.One);
            typeof(GridSystem).GetField("_cellConnections", Fields).SetValue(_grid, new HashSet<CellConnection>());
            typeof(GridSystem).GetField("_adj", Fields).SetValue(_grid, new Dictionary<Vector3I, HashSet<Vector3I>>());
            var cells = new GridCell[20][,];
            for (int y = 0; y < 20; y++)
            {
                cells[y] = new GridCell[12, 12];
                for (int x = 0; x < 12; x++)
                    for (int z = 0; z < 12; z++)
                        cells[y][x, z] = new GridCell(new(x, y, z), new(x + .5f, y, z + .5f), new(x + .5f, y + .5f, z + .5f),
                            y == 0 ? Enums.GridCellState.Ground : Enums.GridCellState.Air,
                            Enums.FogState.Visible, _inventory.GetInventoryGrid(Enums.InventoryType.Ground));
            }
            typeof(GridSystem).GetProperty("GridCells").SetValue(_grid, cells);
            _manager = new GridObjectManager();
            AddChild(_manager);
            _player = Team(Enums.UnitTeam.Player);
            _enemy = Team(Enums.UnitTeam.Enemy);
            _manager.GetGridObjectTeamHolders()[Enums.UnitTeam.Player] = _player;
            _manager.GetGridObjectTeamHolders()[Enums.UnitTeam.Enemy] = _enemy;

            await CheckRealPrefabStun();
            await CheckCollapseAndRecovery();
            await CheckBlockedAndCarried();
            await CheckWoundsAndDeath();
            await CheckSaveLoad();
            await CheckRecovery();
            await CheckStunRod();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (GridCell cell in _grid.AllGridCells)
                foreach (var entry in cell.InventoryGrid.UniqueItems)
                    if (GodotObject.IsInstanceValid(entry.item) && entry.item.GetParent() == null) entry.item.Free();
            _manager.Free();
            _grid.Free();
            _inventory.Free();
            terrain.Free();
            game.Free();
            GD.Print($"PASS: unit condition regression ({_checks} assertions).");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PrintErr(exception);
            GetTree().Quit(1);
        }
    }

    private GridObjectTeamHolder Team(Enums.UnitTeam team)
    {
        var holder = new GridObjectTeamHolder();
        holder.Set("Team", (int)team);
        _manager.AddChild(holder);
        holder.Setup();
        return holder;
    }

    private GridCell Cell(int x, int z) => _grid.GetGridCell(new Vector3I(x, 0, z));
    private async Task<GridObject> Unit(int x, int z, Enums.UnitTeam team = Enums.UnitTeam.Player, bool withActions = false)
    {
        var unit = GD.Load<PackedScene>("res://Tests/Fixtures/ConditionUnit.tscn").Instantiate<GridObject>();
        if (withActions)
        {
            var actions = new GridObjectActions();
            typeof(GridObjectActions).GetProperty("ActionDefinitions").SetValue(actions,
                new ActionDefinition[] { new MoveActionDefinition(), new RotateActionDefinition() });
            unit.AddChild(actions);
            unit.GetNode("GridObjectNodes/Stats").AddChild(new GridObjectStat(Enums.Stat.Stamina, 80, 0, 80) { Name = "Stamina" });
            unit.visualMesh = new Node3D();
            unit.AddChild(unit.visualMesh);
        }
        AddChild(unit);
        unit.GlobalPosition = Cell(x, z).WorldCenter;
        await unit.Initialize(team, Cell(x, z));
        await unit.TeamHolder.AddGridObject(unit);
        return unit;
    }

    private InventoryGrid Backpack(GridObject unit)
    {
        unit.TryGetGridObjectNode<GridObjectInventory>(out var inventory);
        return inventory.InventoryGrids[Enums.InventoryType.Backpack];
    }
    private float TU(GridObject unit)
    {
        unit.TryGetGridObjectNode<GridObjectStatHolder>(out var stats);
        stats.TryGetStat(Enums.Stat.TimeUnits, out var tu);
        return tu.CurrentValue;
    }
    private void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private async Task Tick(Enums.UnitTeam team)
    {
        var turn = new Turn();
        turn.Set("team", (int)team);
        var segment = new ProcessGridObjectsSegment();
        await segment.SetupCall(turn);
        await segment.ExecuteCall();
    }

    private async Task CheckRealPrefabStun()
    {
        var actionManager = new ActionManager();
        AddChild(actionManager);
        var turns = new TurnManager();
        AddChild(turns);
        var turn = new Turn();
        turn.Set("team", (int)Enums.UnitTeam.Player);
        typeof(TurnManager).GetField("turns", Fields).SetValue(turns, new[] { turn });
        foreach (string scene in new[] { "Unit", "Enemy", "lamprey" })
        {
            var terrainMarker = new GridCellStateOverride();
            AddChild(terrainMarker);
            Cell(8, 4).gridObjects.Insert(0, terrainMarker);
            var target = GD.Load<PackedScene>($"res://Scenes/GridObjects/{scene}.tscn").Instantiate<GridObject>();
            AddChild(target);
            target.GlobalPosition = Cell(8, 4).WorldCenter;
            await target.Initialize(Enums.UnitTeam.Enemy, Cell(8, 4));
            await _enemy.AddGridObject(target);
            Require(target.Condition?.Stun != null, $"{scene}: condition/stun initialized");
            var attacker = await Unit(9, 4, withActions: true);
            var rod = ItemData.CreateItem(_inventory.GetItemData(113));
            rod.ItemData.TryGetItemActionDefinition<MeleeAttackActionDefinition>(out var definition);
            for (int hit = 1; hit <= 3; hit++)
            {
                attacker.TryGetGridObjectNode<GridObjectStatHolder>(out var attackerStats);
                attackerStats.TryGetStat(Enums.Stat.TimeUnits, out var timeUnits);
                timeUnits.SetValue(60);
                definition.Item = rod;
                Require(definition.GetTarget(attacker, Cell(8, 4)) == target, $"{scene}: ignore terrain marker for target selection");
                Require(await actionManager.TryTakeAction(definition, attacker, Cell(9, 4), Cell(8, 4)), $"{scene}: full stun action completes");
                Require(target.Condition.Stun.CurrentValue == hit * 40, $"{scene}: hit {hit} should accumulate {hit * 40} stun, got {target.Condition.Stun.CurrentValue}");
            }
            Require(target.Condition.State == Enums.UnitCondition.Unconscious, $"{scene}: three hits must collapse 100-health unit");
            target.Condition.DestroyBody();
            _enemy.GridObjects[Enums.GridObjectState.Inactive].Remove(target);
            foreach (var cell in _grid.AllGridCells) cell.gridObjects.Remove(target);
            target.Free();
            Cell(8, 4).gridObjects.Remove(terrainMarker);
            terrainMarker.Free();
            attacker.GridPositionData.SetGridCell(null);
            _player.GridObjects[Enums.GridObjectState.Active].Remove(attacker);
            foreach (var cell in _grid.AllGridCells) cell.gridObjects.Remove(attacker);
            attacker.Free();
            rod.Free();
        }
        actionManager.Free();
        turns.Free();
    }

    private async Task CheckCollapseAndRecovery()
    {
        var unit = await Unit(2, 2);
        var condition = unit.Condition;
        var health = condition.Health;
        var weapon = ItemData.CreateItem(_inventory.GetItemData(1));
        Require(Backpack(unit).TryAddItem(weapon, 1), "Equip test weapon");
        _player.SetSelectedGridObject(unit);
        Require(condition.Stun.CurrentValue == 0, "Stun must start at zero");
        condition.ApplyStun(29);
        Require(unit.CanAct, "Stun below health must not collapse");
        condition.ApplyStun(1);
        var body = condition.Body;
        Require(condition.State == Enums.UnitCondition.Unconscious && !unit.CanAct, "Equality must collapse");
        Require(ReferenceEquals(body.LinkedUnit, unit) && ReferenceEquals(health, condition.Health), "Preserve original unit and stats");
        Require(unit.GridPositionData.AnchorCell == null && !Cell(2, 2).gridObjects.Contains(unit), "Release entire grid footprint");
        Require(unit.CollisionLayer == 0 && !unit.Visible, "Disable physics and standing visual");
        Require(_player.CurrentGridObject == null && !_player.IsGridObjectActive(unit), "Clear final selected unit");
        Require(Backpack(unit).ItemCount == 0 && Cell(2, 2).InventoryGrid.HasItem(weapon), "Drop equipment");
        Require(body.currentGrid == Cell(2, 2).InventoryGrid && TU(unit) == 0, "Place body on ground and clear TU");
        Require(!Backpack(unit).TryAddItem(body, 1), "Cannot insert the same body in two inventories");
        Require(!InventoryGrid.TryTransferItem(body.currentGrid, Backpack(unit), body, 1), "Cannot put a body in its own inventory");
        condition.Evaluate();
        Require(Cell(2, 2).InventoryGrid.UniqueItems.Count(entry => entry.item is UnitBodyItem) == 1, "Repeated evaluation creates no duplicate");
        Require(!new RotateActionDefinition().CanTakeAction(unit, Cell(2, 2), Cell(2, 3), out _, out _), "Reject unconscious actions");
        await Tick(Enums.UnitTeam.Player);
        Require(unit.CanAct && condition.Stun.CurrentValue == 29 && condition.Body == null, "Inactive units recover on their team turn");
        Require(TU(unit) == 60 && unit.CollisionLayer == 1 && unit.GridPositionData.AnchorCell == Cell(2, 2), "Restore original unit with TU and collision");
        Require(!Cell(2, 2).InventoryGrid.HasItem(body) && Backpack(unit).ItemCount == 0, "Remove proxy and keep equipment on floor");
        condition.ApplyStun(1);
        Require(condition.ApplyStimulant() && TU(unit) == 0, "Stimulants remove four stun and give zero TU");
        condition.ApplyStun(24);
        condition.ApplyStimulant(); // Still above health; later natural waking must have TU.
        for (int i = 0; i < 17; i++) condition.ProcessTurn();
        Require(unit.CanAct && TU(unit) == 60, "An earlier insufficient stimulant must not penalize natural recovery");
        unit.SetIsActive(false);
        _player.SetUnitActive(unit, false);
    }

    private async Task CheckBlockedAndCarried()
    {
        var unit = await Unit(5, 5);
        unit.Condition.ApplyStun(30);
        var body = unit.Condition.Body;
        var carrier = await Unit(8, 8, Enums.UnitTeam.Enemy);
        Require(InventoryGrid.TryTransferItem(body.currentGrid, Backpack(carrier), body, 1), "Carry body using existing transfer");
        Require(body.GetWorldCell() == Cell(8, 8), "Recovery follows carrier's current location");
        carrier.GridPositionData.SetGridCell(Cell(9, 8));
        carrier.GlobalPosition = Cell(9, 8).WorldCenter;
        Require(body.GetWorldCell() == Cell(9, 8), "Moving carrier moves body's recovery origin");
        unit.Condition.ProcessTurn();
        Require(unit.CanAct && unit.GridPositionData.AnchorCell == Cell(9, 7), "Carried unit wakes north of occupied carrier cell");
        Require(!Backpack(carrier).HasItem(body), "Remove carried body on recovery");
        unit.Condition.ApplyStun(1);
        body = unit.Condition.Body;
        var obstacles = new List<GridCell>();
        for (int x = 8; x <= 10; x++)
            for (int z = 6; z <= 8; z++)
            {
                var cell = Cell(x, z);
                cell.SetStateWithoutConnectionUpdate(cell.state | Enums.GridCellState.Obstructed);
                obstacles.Add(cell);
            }
        Require(!unit.Condition.ApplyStimulant(), "No recovery when every candidate is blocked");
        Require(unit.Condition.Body == body && body.currentGrid != null, "Keep body while waiting for space");
        Cell(9, 6).SetStateWithoutConnectionUpdate(Enums.GridCellState.Ground);
        unit.Condition.ProcessTurn();
        Require(unit.CanAct && TU(unit) == 0 && unit.GridPositionData.AnchorCell == Cell(9, 6), "Retry blocked stimulant recovery with zero TU");
        foreach (var cell in obstacles) cell.SetStateWithoutConnectionUpdate(Enums.GridCellState.Ground);
    }

    private async Task CheckWoundsAndDeath()
    {
        var unit = await Unit(3, 5);
        unit.Condition.ApplyStun(25);
        unit.Condition.Health.ApplyDamage(6, false);
        Require(unit.Condition.State == Enums.UnitCondition.Unconscious, "Lowering health below existing stun collapses");
        var savedHealth = unit.Condition.Health.Save();
        savedHealth["fatalWounds"] = new Godot.Collections.Dictionary<string, Variant> { ["Torso"] = 2 };
        unit.Condition.Health.Load(savedHealth);
        await Tick(Enums.UnitTeam.Player);
        Require(unit.Condition.Health.CurrentValue == 22 && unit.Condition.Stun.CurrentValue == 24, "Bleeding and stun recovery both run while downed");
        Require(unit.Condition.Health.HealFatalWound(Enums.BodyPart.Torso), "Heal wounds through original component while downed");
        unit.Condition.ApplyStun(100);
        unit.Condition.Health.SetValue(1);
        unit.Condition.ProcessTurn();
        Require(unit.Condition.State == Enums.UnitCondition.Dead && unit.Condition.Body.IsDead, "Bleeding to zero kills existing body");
        Require(!unit.Condition.ApplyStimulant(10000), "Dead unit never wakes");
        unit.Condition.DestroyBody();
        Require(unit.Condition.Body == null && unit.Condition.Health.CurrentValue == 0, "Destroying body does not delete unit data");
        var blastUnit = await Unit(5, 9);
        blastUnit.Condition.ApplyStun(30);
        var blast = new ExplodeActionBase(blastUnit, Cell(5, 9), Cell(5, 9), null, null, new(),
            new() { [Enums.Stat.Health] = 1 }, radius: 2);
        blast.OnDelayComplete();
        Require(blastUnit.Condition.State == Enums.UnitCondition.Dead && blastUnit.Condition.Body == null, "HE blast destroys unconscious body");
    }

    private async Task CheckSaveLoad()
    {
        var unit = await Unit(2, 9);
        unit.Condition.ApplyStun(40);
        string id = unit.UnitId;
        var carrier = await Unit(7, 2, Enums.UnitTeam.Enemy);
        Require(InventoryGrid.TryTransferItem(unit.Condition.Body.currentGrid, Backpack(carrier), unit.Condition.Body, 1), "Carry body across teams before save");
        var ground = _grid.AllGridCells.Where(cell => cell.InventoryGrid.ItemCount > 0)
            .ToDictionary(cell => cell, cell => cell.InventoryGrid.SaveContents());
        var playerData = GD.BytesToVar(GD.VarToBytes(_player.Save())).AsGodotDictionary<string, Variant>();
        var enemyData = GD.BytesToVar(GD.VarToBytes(_enemy.Save())).AsGodotDictionary<string, Variant>();
        // Reload through the actual roster and inventory serializers.
        foreach (var holder in new[] { _player, _enemy })
            foreach (var old in holder.GridObjects.Values.SelectMany(list => list)) old.GridPositionData.SetGridCell(null);
        foreach (var cell in _grid.AllGridCells)
        {
            var oldItems = cell.InventoryGrid.UniqueItems.ToArray();
            cell.InventoryGrid.ClearInventory();
            foreach (var entry in oldItems)
                if (entry.item.GetParent() == null) entry.item.Free();
        }
        await _player.LoadAsync(playerData);
        await _enemy.LoadAsync(enemyData);
        foreach (var entry in ground) entry.Key.InventoryGrid.LoadContents(entry.Value);
        _manager.RestoreBodyLinks();
        var restored = _player.GridObjects[Enums.GridObjectState.Inactive].Single(candidate => candidate.UnitId == id);
        Require(restored.Condition.State == Enums.UnitCondition.Unconscious && restored.Condition.Stun.CurrentValue == 40, "Restore consciousness and stun");
        Require(restored.Condition.Body?.LinkedUnit == restored, "Resolve saved body to exactly the restored unit");
        Require(restored.Condition.Body.GetWorldCell() == Cell(7, 2), "Restore cross-team carrier ownership");
        Require(restored.GridPositionData.AnchorCell == null && restored.CollisionLayer == 0, "Loaded inactive unit occupies no cells or physics");
        Require(restored.Condition.ApplyStimulant(20), "Loaded body can recover");
        Require(restored.CanAct && restored.Condition.Body == null && TU(restored) == 0, "Revive loaded original once");
    }

    private async Task CheckRecovery()
    {
        var unit = await Unit(1, 7);
        unit.Condition.ApplyStun(40);
        var mission = new EliminateMission("Test", "", Enums.MissionType.Eliminate, 1, 1, 0);
        var success = MissionRecoveryResolver.Resolve(mission, Enums.MissionStatus.Successful);
        Require(success.RecoveredUnits.Any(saved => saved["UnitId"].AsString() == unit.UnitId), "Recover unconscious soldiers after victory");
        var abort = MissionRecoveryResolver.Resolve(mission, Enums.MissionStatus.Aborted);
        Require(!abort.RecoveredUnits.Any(saved => saved["UnitId"].AsString() == unit.UnitId), "Do not recover bodies left outside extraction");
        Cell(1, 7).SetUnitSpawnState(Enums.UnitTeam.Player);
        abort = MissionRecoveryResolver.Resolve(mission, Enums.MissionStatus.Aborted);
        Require(abort.RecoveredUnits.Any(saved => saved["UnitId"].AsString() == unit.UnitId), "Recover bodies on extraction cells");
        Require(!success.RecoveredItems.ContainsKey(-2), "Never sell or stack bodies as equipment");
        var savedUnit = success.RecoveredUnits.Single(saved => saved["UnitId"].AsString() == unit.UnitId);
        var condition = savedUnit["Nodes"].AsGodotDictionary<string, Variant>()["Condition"].AsGodotDictionary<string, Variant>();
        Require(condition["state"].AsInt32() == (int)Enums.UnitCondition.Conscious, "Clear tactical stun on evacuation");
    }

    private async Task CheckStunRod()
    {
        var wielder = await Unit(3, 10);
        var target = await Unit(4, 10, Enums.UnitTeam.Enemy);
        Item rod = ItemData.CreateItem(_inventory.GetItemData(113));
        Require(rod.ItemData.TryGetItemActionDefinition<MeleeAttackActionDefinition>(out var definition), "Stun rod registered with melee action");
        var attack = new MeleeAttackActionBase(wielder, Cell(3, 10), Cell(4, 10), definition, new()) { Item = rod };
        await (Task)typeof(MeleeAttackActionBase).GetMethod("Execute", Fields).Invoke(attack, null);
        Require(target.Condition.State == Enums.UnitCondition.Unconscious && target.Condition.Health.CurrentValue == 30 &&
            target.Condition.Stun.CurrentValue == 40 && target.Condition.Health.GetTotalFatalWounds() == 0,
            "Real stun-rod attack collapses without health damage or wounds");
        rod.Free();
    }
}
