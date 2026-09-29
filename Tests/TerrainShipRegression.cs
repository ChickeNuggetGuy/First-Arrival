using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class TerrainShipRegression : Node3D
{
	public override async void _Ready()
	{
		try
		{
			var game = new GameManager { mapSize = Vector2I.One };
			typeof(Manager<GameManager>).GetProperty("Instance").SetValue(null, game);
			var battle = GD.Load<PackedScene>("res://Scenes/GameScenes/BattleScene.tscn").Instantiate();
			var configuredTerrain = battle.GetNode<MeshTerrainGenerator>("Node/MeshTerrainGenerator");
			var definitions = configuredTerrain.Get("structureDefinitions");
			var units = battle.GetNode<GridObjectManager>("Node/GridObjectManager");
			units.GetParent().RemoveChild(units);
			var grid = battle.GetNode<GridSystem>("Node/GridSystem");
			grid.GetParent().RemoveChild(grid);
			battle.Free();
			AddChild(units);
			AddChild(grid);
			grid.SetProcess(false);
			grid.Set("DebugMode", false);
			var inventory = new InventoryManager();
			AddChild(inventory);
			await inventory.SetupCall(false);

			foreach (string block in new[] { "CityChunk_base", "CityChunk_01" })
			{
				var terrain = CreateTerrain(definitions, block);
				await terrain.SetupCall(false);
				await terrain.ExecuteCall(false);
				Node3D ship = FindShip(terrain);
				Check(ship != null, $"{block} must place the ship on a one-block city map.");
				Check(ship.FindChild("CellStateOveride", true, false) is GridCellStateOverride,
					"The actual ship must carry its player spawn override.");
				Check(ship.GlobalPosition.Y < 1f, "The current city scenes must offer a ground-level landing site.");
				await grid.SetupCall(false);
				await units.SetupCall(false);
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
				await grid.ExecuteCall(false);
				Check(grid.AllGridCells.Any(cell => cell.IsWalkable && cell.UnitTeamSpawn == Enums.UnitTeam.Player),
					"The ship must produce walkable player spawn cells in the actual city grid.");
				GridObjectTeamHolder player = units.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
				for (int i = 0; i < game.unitCounts.X; i++)
					await (Task)typeof(GridObjectManager).GetMethod("TrySpawnGridObject", BindingFlags.Instance | BindingFlags.NonPublic)
						.Invoke(units, new object[] { player.unitPrefabs[0], Enums.UnitTeam.Player });
				Check(player.GridObjects[Enums.GridObjectState.Active].Count == game.unitCounts.X,
					"Every starting player unit must spawn using the normal spawn routine.");
				foreach (GridObject unit in player.GridObjects[Enums.GridObjectState.Active].ToArray())
					unit.Free();
				player.GridObjects[Enums.GridObjectState.Active].Clear();
				var saved = terrain.Save();
				Transform3D transform = ship.GlobalTransform;
				string chunkPath = terrain.GetChunkData(0, 0).chunkGOIndex;
				terrain.Free();
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

				terrain = CreateTerrain(definitions, block == "CityChunk_base" ? "CityChunk_01" : "CityChunk_base");
				await terrain.LoadCall(saved);
				await terrain.SetupCall(true);
				await terrain.ExecuteCall(true);
				ship = FindShip(terrain);
				Check(ship != null && ship.GlobalTransform.IsEqualApprox(transform),
					"Reload must restore the ship's exact transform.");
				Check(terrain.GetChunkData(0, 0).chunkGOIndex == chunkPath,
					"Reload must restore the same city block beneath the ship.");
				GD.Print($"PASS: city ship placement and reload on {block}, position {ship.GlobalPosition}.");
				terrain.Free();
				await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			}
			units.Free();
			grid.Free();
			inventory.Free();
			game.Free();
			GD.Print("PASS: terrain ship regression.");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PrintErr(error);
			GetTree().Quit(1);
		}
	}

	private MeshTerrainGenerator CreateTerrain(Variant definitions, string block)
	{
		var terrain = new MeshTerrainGenerator { cellSize = Vector2.One };
		terrain.Set("mapType", (int)Enums.ChunkType.Urban);
		terrain.Set("structureDefinitions", definitions);
		terrain.Set("autoLoadChunkPrefabsFromFolders", false);
		terrain.Set("chunkPrefabs", new Godot.Collections.Dictionary<Enums.ChunkType, Godot.Collections.Array>
		{
			[Enums.ChunkType.Urban] = new Godot.Collections.Array { $"res://Scenes/Chunks/Urban/{block}.tscn" }
		});
		AddChild(terrain);
		return terrain;
	}

	private Node3D FindShip(MeshTerrainGenerator terrain) => GetTree()
		.GetNodesInGroup("GeneratedTerrainStructures").OfType<Node3D>()
		.FirstOrDefault(terrain.IsAncestorOf);

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
