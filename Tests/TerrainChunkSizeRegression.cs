using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

// Run without the game's autoloads, following the other isolated regression scenes.
public partial class TerrainChunkSizeRegression : Node3D
{
	public override async void _Ready()
	{
		try
		{
			var game = new GameManager { mapSize = new Vector2I(2, 3) };
			typeof(Manager<GameManager>).GetProperty("Instance").SetValue(null, game);
			var terrain = new MeshTerrainGenerator { cellSize = Vector2.One };
			terrain.Set("autoLoadChunkPrefabsFromFolders", false);
			terrain.Set("mapType", (int)Enums.ChunkType.Urban);
			terrain.Set("chunkPrefabs", new Godot.Collections.Dictionary<Enums.ChunkType, Godot.Collections.Array>
			{
				[Enums.ChunkType.Urban] = new Godot.Collections.Array
				{
					"res://Tests/Fixtures/CityBlock.tscn"
				}
			});
			AddChild(terrain);
			var grid = new GridSystem();
			AddChild(grid);
			grid.SetProcess(false);

			await terrain.SetupCall(false);
			await grid.SetupCall(false);
			await terrain.ExecuteCall(false);
			Check(grid.GetGridSize() == new Vector3I(80, 20, 120), "City grid must cover every 40-cell block.");
			Check(terrain.terrainHeights == null, "Cities must not generate a procedural heightmap.");
			for (int x = 0; x < 2; x++)
			for (int z = 0; z < 3; z++)
			{
				Node3D block = terrain.GetChunkData(x, z).GetChunkNode();
				Check(block.Position == new Vector3(x * 40, 0, z * 40), "City blocks must tile without overlap.");
				Check(block.Scale == Vector3.One, "Handmade blocks must keep their authored scale.");
			}
			Check(terrain.IsManMadeChunkAtWorld(79.5f, 119.5f, game), "Far corner must belong to the city.");
			Check(!terrain.IsManMadeChunkAtWorld(80, 119.5f, game), "X boundary must be outside the city.");
			Check(!terrain.IsManMadeChunkAtWorld(79.5f, 120, game), "Z boundary must be outside the city.");
			Check(!terrain.IsManMadeChunkAtWorld(-0.5f, 0, game), "Negative coordinates must remain outside.");

			terrain.urbanChunkSize = 60;
			Check(grid.GetGridSize().X == 120, "Custom city size must also reach the gameplay grid.");
			terrain.cellSize = new Vector2(2, 1);
			Check(terrain.GetMapCellSize().X == 120, "Chunk world size must include horizontal cell spacing.");
			terrain.cellSize = Vector2.One;
			foreach (Enums.ChunkType type in new[] { Enums.ChunkType.Grassland, Enums.ChunkType.Forest, Enums.ChunkType.Mountain })
			{
				terrain.Set("mapType", (int)type);
				await terrain.SetupCall(false);
				Check(grid.GetGridSize() == new Vector3I(40, 20, 60), "Procedural maps must retain 20-cell chunks.");
				Check(terrain.terrainHeights.GetLength(0) == 41 && terrain.terrainHeights.GetLength(1) == 61,
					"Procedural heightmap must match the gameplay grid plus edge vertices.");
			}
			terrain.chunkSize = 24;
			await terrain.SetupCall(false);
			Check(grid.GetGridSize().X == 48 && terrain.terrainHeights.GetLength(0) == 49,
				"Existing custom procedural sizes must remain supported.");
			float savedHeight = terrain.terrainHeights[12, 15].Y;
			await terrain.LoadCall(terrain.Save());
			await terrain.SetupCall(true);
			Check(terrain.terrainHeights.GetLength(0) == 49 && terrain.terrainHeights[12, 15].Y == savedHeight,
				"Procedural heightmaps must survive the shared structure save/load path.");
			terrain.Set("mapType", (int)Enums.ChunkType.Urban);
			terrain.urbanChunkSize = 0;
			Check(terrain.ActiveChunkSize == 1, "Invalid dimensions must not cause division by zero.");

			GD.Print("PASS: terrain chunk sizes (city placement, grid bounds, custom sizes, procedural heightmaps).");
			grid.Free();
			terrain.Free();
			game.Free();
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PrintErr(error);
			GetTree().Quit(1);
		}
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
