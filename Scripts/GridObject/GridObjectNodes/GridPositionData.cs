using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass, Tool]
public partial class GridPositionData : GridObjectNode
{
	[ExportGroup("Settings")]
	[Export] public GridShape gridShape;
	[Export] bool debugMode = false;
	
	[Export] public Vector3 CellSize { get; set; } = new Vector3(1, 1, 1);
	[Export] public Vector3 GridWorldOrigin { get; set; } = Vector3.Zero;
	
	[ExportGroup("State")]
	[Export] public Enums.Direction Direction { get; private set; } = Enums.Direction.North;

	// The primary cell (pivot) this object is anchored to
	public GridCell AnchorCell { get; private set; }
	
	// A list of all cells currently occupied by this object
	public List<GridCell> OccupiedCells { get; private set; } = new List<GridCell>();

	[Signal] public delegate void GridPositionDataUpdatedEventHandler(GridPositionData gridPositionData);
	[Signal] public delegate void GridCellPositionUpdatedEventHandler(Vector3I newGridCoordinates);

	public override void _EnterTree()
	{
		base._EnterTree();
		if (gridShape == null) gridShape = new GridShape();
	}
	
	protected override void Setup()
	{
		if (parentGridObject == null)
		{
			parentGridObject = this.GetParent() as GridObject;
		}
		
		// Attempt to sync settings from GridSystem if available
		if (GridSystem.Instance != null)
		{
			var sysCellSize = GridSystem.Instance.CellSize;
			CellSize = new Vector3(sysCellSize.X, sysCellSize.Y, sysCellSize.X);
			GridWorldOrigin = GridSystem.Instance.GridWorldOrigin;
		}

		if (!GridSystem.Instance.TryGetGridCellFromWorldPosition(this.Position, out GridCell gridCell, true))
		{
			return;
		}
		
		SetGridCell(gridCell);
	}

	#region Logic & State Management

	/// <summary>
	/// Places the object on the grid at the specified anchor cell.
	/// Calculates all occupied cells based on Shape + Direction.
	/// </summary>
	public void SetGridCell(GridCell newAnchorCell)
	{
		//Clear previous occupation from the grid
		ClearGridOccupation();

		AnchorCell = newAnchorCell;

		// If we aren't assigning a cell (just clearing), or system is missing, stop here.
		if (AnchorCell == null) return;
		if (GridSystem.Instance == null) return;

		// 2. Calculate which coordinates are valid based on Anchor + Direction + Shape
		List<Vector3I> targetCoords = CalculateOccupiedCoordinates(AnchorCell.gridCoordinates, Direction);
		
		bool isWalkThrough = parentGridObject.gridObjectSettings.HasFlag(Enums.GridObjectSettings.CanWalkThrough);

		// 3. Occupy the new cells
		foreach (Vector3I coord in targetCoords)
		{
			// Ask the system for the actual cell instance
			GridCell cell = GridSystem.Instance.GetGridCell(coord);
			
			// If the shape extends off the map, ignore those cells
			if (cell == null) continue;

			OccupiedCells.Add(cell);

			// Logic specific to the root cell vs other cells
			bool isRoot = (coord == AnchorCell.gridCoordinates);

			// Determine the State Mask
			var tempNewState = cell.state;
			
			if (isWalkThrough)
				tempNewState &= ~Enums.GridCellState.Obstructed;
			else
				tempNewState &= ~Enums.GridCellState.Empty;

			if (!isRoot)
				tempNewState &= ~Enums.GridCellState.Empty;

			// Add the object to the cell
			cell.AddGridObject(parentGridObject, tempNewState, rebuildConnections: !isWalkThrough);
		}

		parentGridObject.GlobalPosition = AnchorCell.worldCenter;
		EmitSignal(SignalName.GridPositionDataUpdated, this);
		EmitSignal(SignalName.GridCellPositionUpdated, AnchorCell.gridCoordinates);
	}

	/// <summary>
	/// Updates the direction and refreshes the grid occupation if the object is already placed.
	/// </summary>
	public void SetDirection(Enums.Direction direction)
	{
		if (Direction == direction) return;
		
		Direction = direction;
		
		// If we are currently on the grid, we need to lift up and place down again
		// because rotating changes which cells we occupy.
		if (AnchorCell != null)
		{
			SetGridCell(AnchorCell);
		}
	}

	/// <summary>
	/// Removes this object from all currently occupied cells.
	/// </summary>
	private void ClearGridOccupation()
	{
		if (OccupiedCells.Count == 0) return;

		foreach (var cell in OccupiedCells)
		{
			if (cell == null) continue;
			
			// Logic to restore cell state
			cell.RestoreOriginalState();
			cell.RemoveGridObject(parentGridObject, cell.originalState, rebuildConnections: false);
		}
		
		OccupiedCells.Clear();
	}

	#endregion

	#region Math & Coordinate Calculation

	/// <summary>
	/// Returns the absolute Grid Coordinates this object occupies based on an anchor, rotation, and shape.
	/// Used by both Logic (SetGridCell) and Visuals (_Process).
	/// </summary>
	public List<Vector3I> CalculateOccupiedCoordinates(Vector3I anchor, Enums.Direction dir)
	{
		List<Vector3I> results = new List<Vector3I>();
		if (gridShape == null) return results;

		// Iterate over the "Local" shape definition
		int sizeX = gridShape.GridSizeX;
		int sizeY = gridShape.GridSizeY;
		int sizeZ = gridShape.GridSizeZ;

		for (int y = 0; y < sizeY; y++)
		{
			for (int x = 0; x < sizeX; x++)
			{
				for (int z = 0; z < sizeZ; z++)
				{
					// If the shape doesn't exist at this local index, skip
					if (!gridShape.GetGridShapeCell(x, y, z)) continue;

					//Calculate offset relative to the Root Cell
					int relX = x - gridShape.RootCellCoordinates.X;
					int relZ = z - gridShape.RootCellCoordinates.Y; // Y in Vector2I is Z depth
					
					//Rotate that offset
					Vector2I rotatedOffset = RotateOffset(relX, relZ, dir);

					//Add to anchor
					Vector3I finalCoord = new Vector3I(
						anchor.X + rotatedOffset.X,
						anchor.Y + y, // Vertical height usually doesn't rotate 
						anchor.Z + rotatedOffset.Y  // 2D Y becomes 3D Z
					);
					
					results.Add(finalCoord);
				}
			}
		}
		return results;
	}

	/// <summary>
	/// Rotates a 2D vector (X, Z) by 90-degree increments.
	/// </summary>
	private Vector2I RotateOffset(int x, int z, Enums.Direction dir)
	{
		// Standard 2D Rotation matrix for 90 degree steps
		return dir switch
		{
			Enums.Direction.North => new Vector2I(x, z),
			Enums.Direction.East  => new Vector2I(-z, x),
			Enums.Direction.South => new Vector2I(-x, -z),
			Enums.Direction.West  => new Vector2I(z, -x),
			_ => new Vector2I(x, z)
		};
	}

	public Enums.Direction GetNearestDirectionFromRotation(float rotationYRadians)
	{
		float angle = Mathf.PosMod(rotationYRadians, Mathf.Tau);
		// 0 is North (-Z) usually.
		// Ranges: 
		// North: 315 - 45 (or roughly 5.5rad - 0.78rad)
		// East:  45 - 135
		// South: 135 - 225
		// West:  225 - 315
		
		if (angle < Mathf.Pi * 0.25f || angle >= Mathf.Pi * 1.75f) return Enums.Direction.North;
		if (angle < Mathf.Pi * 0.75f) return Enums.Direction.East;
		if (angle < Mathf.Pi * 1.25f) return Enums.Direction.South;
		return Enums.Direction.West;
	}

	#endregion

	#region Editor Debugging

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!Engine.IsEditorHint() || !debugMode || gridShape == null) return;

		// 1. Update Direction automatically in Editor
		Enums.Direction currentDir = GetNearestDirectionFromRotation(GlobalRotation.Y);
		if (currentDir != Direction)
		{
			Direction = currentDir; 
			// Note: We don't call SetGridCell here to avoid messing with actual game logic in Editor
		}

		// 2. Calculate where the Anchor IS based on world position
		Vector3 worldPos = GlobalPosition;
		Vector3 localPos = worldPos - GridWorldOrigin;
		
		// Align this math with how GridSystem determines cell coordinates
		Vector3I calculatedAnchor = new Vector3I(
			Mathf.FloorToInt(localPos.X / CellSize.X), 
			Mathf.FloorToInt(localPos.Y / CellSize.Y), 
			Mathf.FloorToInt(-localPos.Z / CellSize.Z) // Assuming Z is inverted in your grid system
		);

		// 3. Get Cells using the EXACT SAME function as logic
		List<Vector3I> coordsToDraw = CalculateOccupiedCoordinates(calculatedAnchor, Direction);

		// 4. Draw
		foreach (var coord in coordsToDraw)
		{
			// Convert Grid Coord back to World Center for drawing
			// World = Origin + (Grid + 0.5) * Size
			Vector3 drawPos = GridWorldOrigin + new Vector3(
				(coord.X + 0.5f) * CellSize.X, 
				(coord.Y + 0.5f) * CellSize.Y, 
				-(coord.Z + 0.5f) * CellSize.Z 
			); 
			
			bool isRoot = (coord == calculatedAnchor);
			Color color = isRoot ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
			
			// If we are strictly checking grid bounds (visualize valid vs invalid)
			bool isValid = true; 
			if (GridSystem.Instance != null && GridSystem.Instance.GetGridCell(coord) == null)
			{
				isValid = false;
				color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Gray for out of bounds
			}
			
			if (isValid)
			{
				DebugDraw3D.DrawBox(drawPos, Quaternion.Identity, CellSize * 0.9f, color, true);
			}
		}
	}
	#endregion

	#region Saving & Loading

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data =  new Godot.Collections.Dictionary<string, Variant>();

		if (AnchorCell != null)
		{
			data["HasPosition"] = true;
			data["GridX"] = AnchorCell.gridCoordinates.X;
			data["GridY"] = AnchorCell.gridCoordinates.Y;
			data["GridZ"] = AnchorCell.gridCoordinates.Z;
			data["Direction"] = (int)Direction;
		}
		else
		{
			data["HasPosition"] = false;
		}

		if (gridShape != null)
		{
			data["GridShapePath"] = gridShape.ResourcePath;
		}

		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{

		// Load shape first
		if (data.ContainsKey("GridShapePath"))
		{
			string path = data["GridShapePath"].AsString();
			var loadedShape = GD.Load<GridShape>(path);
			if (loadedShape != null) gridShape = loadedShape;
		}

		// Load Position Data
		if (data.ContainsKey("HasPosition") && data["HasPosition"].AsBool())
		{
			int x = (int)data["GridX"];
			int y = (int)data["GridY"];
			int z = (int)data["GridZ"];

			if (data.ContainsKey("Direction"))
			{
				Direction = (Enums.Direction)(int)data["Direction"];
			}

			// We defer the actual SetGridCell call to the GridObject.Load 
			// or we can attempt to fetch it here if GridSystem is ready.
			if (GridSystem.Instance != null)
			{
				var cell = GridSystem.Instance.GetGridCell(new Vector3I(x, y, z));
				if (cell != null)
				{
					SetGridCell(cell);
				}
			}
		}
	}

	#endregion
}