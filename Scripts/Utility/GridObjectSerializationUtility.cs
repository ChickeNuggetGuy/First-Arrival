using System.Threading.Tasks;
using Godot;

public static class GridObjectSerializationUtility
{
	public static Godot.Collections.Array<
		Godot.Collections.Dictionary<string, Variant>> SaveGridObjects(
		Godot.Collections.Array<GridObject> gridObjects
	)
	{
		var savedUnits =
			new Godot.Collections.Array<
				Godot.Collections.Dictionary<string, Variant>>();

		if (gridObjects == null)
		{
			return savedUnits;
		}

		foreach (var gridObject in gridObjects)
		{
			if (gridObject == null) continue;
			savedUnits.Add(gridObject.Save());
		}

		return savedUnits;
	}

	public static async Task<GridObject> LoadGridObjectAsync(
		Godot.Collections.Dictionary<string, Variant> unitData,
		Node parent,
		bool storeAsInactive = true
	)
	{
		if (unitData == null)
		{
			return null;
		}

		if (!unitData.ContainsKey("Filename"))
		{
			GD.PrintErr("GridObject save data is missing Filename.");
			return null;
		}

		string scenePath = unitData["Filename"].AsString();
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			GD.PrintErr("GridObject Filename is empty.");
			return null;
		}

		PackedScene scene = GD.Load<PackedScene>(scenePath);
		if (scene == null)
		{
			GD.PrintErr($"Could not load GridObject scene: {scenePath}");
			return null;
		}

		GridObject instance = scene.Instantiate<GridObject>();
		if (instance == null)
		{
			GD.PrintErr(
				$"Scene at {scenePath} did not instantiate as GridObject."
			);
			return null;
		}

		parent.AddChild(instance);
		await instance.LoadAsync(unitData);

		if (storeAsInactive)
		{
			instance.SetIsActive(false);
			instance.Visible = false;
		}

		return instance;
	}

	public static async Task<Godot.Collections.Array<GridObject>>
		LoadGridObjectsAsync(
			Godot.Collections.Array<
				Godot.Collections.Dictionary<string, Variant>> savedUnits,
			Node parent,
			bool storeAsInactive = true
		)
	{
		var loadedUnits = new Godot.Collections.Array<GridObject>();

		if (savedUnits == null)
		{
			return loadedUnits;
		}

		foreach (var unitData in savedUnits)
		{
			GridObject instance = await LoadGridObjectAsync(
				unitData,
				parent,
				storeAsInactive
			);

			if (instance != null)
			{
				loadedUnits.Add(instance);
			}
		}

		return loadedUnits;
	}
}