using System;
using Godot;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass, Tool]
public partial class GridPositionData : GridObjectNode
{
	[ExportGroup("Shape Configuration")]
	[Export]
	public GridShape Shape { get; set; }

	[Export] public bool AutoCalculateShape { get; set; } = false;
	[Export] public bool RotateShapeWithDirection { get; set; } = false;
	[Export] public bool RecursiveShapeDetection { get; set; } = true;

	[ExportGroup("Shape Configuration")]
	[Export]
	public bool GenerateShapeNow
	{
		get => false;
		set
		{
			if (!Engine.IsEditorHint() || !value) return;
			CallDeferred(nameof(EditorGenerateShape));
		}
	}

	private void EditorGenerateShape()
	{
		_shapeCalculated = false;
		CalculateShapeFromColliders();
		NotifyPropertyListChanged();
	}

	[ExportGroup("Debug")] [Export] private bool _showDebugInEditor = true;
	[Export] private Color _pivotColor = new Color(0, 1, 0, 0.6f);
	[Export] private Color _occupiedColor = new Color(1, 0.5f, 0, 0.4f);

	[ExportGroup("Runtime State (Read-Only)")]
	[Export]
	public Enums.Direction Direction { get; private set; } = Enums.Direction.North;

	public GridCell AnchorCell { get; private set; }
	public List<GridCell> OccupiedCells { get; private set; } = new();

	[Signal]
	public delegate void PositionChangedEventHandler(GridPositionData data);

	[Signal]
	public delegate void DirectionChangedEventHandler(Enums.Direction newDirection);

	private GridConfiguration _config;
	private bool _shapeCalculated = false;

	public override void _EnterTree()
	{
		base._EnterTree();
		Shape ??= new GridShape();

		if (Engine.IsEditorHint() && AutoCalculateShape)
			CallDeferred(nameof(EditorGenerateShape));
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (!Engine.IsEditorHint() || !_showDebugInEditor || Shape == null)
			return;

		_config = GridConfiguration.GetActive();
		DrawEditorPreview();
	}

	protected override void Setup()
	{
		_config = GridConfiguration.GetActive();

		if (parentGridObject == null)
			parentGridObject = GetParent() as GridObject;

		var facingNode = parentGridObject?.visualMesh ?? parentGridObject;
		Direction = GetDirectionFromRotation(facingNode?.Rotation.Y ?? GlobalRotation.Y);

		if (Engine.IsEditorHint())
			return;

		if (AutoCalculateShape)
			CalculateShapeFromColliders();

		if (GridSystem.Instance == null || _config == null)
			return;

		var anchorCoords = _config.WorldToGrid(GlobalPosition);
		var cell = GridSystem.Instance.GetGridCell(anchorCoords);
		if (cell != null)
			SetGridCell(cell);
	}

	public void CalculateShapeFromColliders()
	{
		if (!Engine.IsEditorHint() && GridSystem.Instance != null)
			_config = GridConfiguration.GetActive();
		else
			_config ??= GridConfiguration.GetActive();

		Node3D targetNode = parentGridObject ?? GetParent() as Node3D;
		if (targetNode == null || _config == null)
			return;

		Shape = GridShape.CreateFromCollisionBounds(
			targetNode,
			_config,
			GlobalPosition,
			RecursiveShapeDetection
		);

		_shapeCalculated = true;

		if (AnchorCell != null)
		{
			SetGridCell(AnchorCell);
		}
	}

	public void RecalculateShape()
	{
		_shapeCalculated = false;
		CalculateShapeFromColliders();

		if (AnchorCell != null)
			SetGridCell(AnchorCell);
	}

	#region Grid Placement

	public List<Vector3I> GetWorldCoordinatesAt(
		Vector3I anchorCoords,
		Enums.Direction direction
	)
	{
		if (Shape == null)
			return new List<Vector3I>();

		// Auto-calculated collision bounds are already generated in their current
		// world orientation. Opt-in directional shapes use an authored South-facing
		// raster and rotate it from gameplay direction.
		return !AutoCalculateShape && RotateShapeWithDirection
			? Shape.GetWorldCoordinates(anchorCoords, direction)
			: Shape.GetWorldCoordinates(anchorCoords);
	}

	public List<GridCell> GetGridCellsAt(
		GridCell anchorCell,
		Enums.Direction direction
	)
	{
		var cells = new List<GridCell>();
		if (anchorCell == null || GridSystem.Instance == null)
			return cells;

		foreach (Vector3I coordinate in GetWorldCoordinatesAt(
			         anchorCell.GridCoordinates,
			         direction))
		{
			GridCell cell = GridSystem.Instance.GetGridCell(coordinate);
			if (cell != null)
				cells.Add(cell);
		}

		return cells;
	}

	/// <summary>
	/// Validates the entire footprint without treating this object's currently
	/// occupied cells as blockers. Support is required only beneath the lowest
	/// occupied cell in each X/Z column, allowing vertical unit volumes.
	/// </summary>
	public bool CanOccupyAt(
		GridCell anchorCell,
		Enums.Direction direction,
		out string reason
	)
	{
		if (anchorCell == null || Shape == null || GridSystem.Instance == null)
		{
			reason = "Anchor, shape, or grid is unavailable";
			return false;
		}

		Enums.Direction shapeDirection = !AutoCalculateShape && RotateShapeWithDirection
			? direction
			: Enums.Direction.None;
		List<Vector3I> occupiedCoordinates = Shape.GetWorldCoordinates(
			anchorCell.GridCoordinates,
			shapeDirection
		);
		if (occupiedCoordinates.Count == 0)
		{
			reason = "Footprint contains no occupied cells";
			return false;
		}

		var supportCoordinates = new HashSet<Vector3I>(
			Shape.GetSupportWorldCoordinates(
				anchorCell.GridCoordinates,
				shapeDirection
			)
		);

		foreach (Vector3I coordinate in occupiedCoordinates)
		{
			GridCell cell = GridSystem.Instance.GetGridCell(coordinate);
			if (cell == null)
			{
				reason = $"Footprint leaves the grid at {coordinate}";
				return false;
			}

			if (supportCoordinates.Contains(coordinate) &&
			    !cell.state.HasFlag(Enums.GridCellState.Ground))
			{
				reason = $"Footprint has no ground support at {coordinate}";
				return false;
			}

			if (cell.state.HasFlag(Enums.GridCellState.Disabled))
			{
				reason = $"Footprint enters a disabled cell at {coordinate}";
				return false;
			}

			if (IsBlockedByAnotherObject(cell))
			{
				reason = $"Footprint is blocked at {coordinate}";
				return false;
			}
		}

		reason = "Success!";
		return true;
	}

	public bool CanOccupyAt(
		GridCell anchorCell,
		Enums.Direction direction
	)
	{
		return CanOccupyAt(anchorCell, direction, out _);
	}

	private bool IsBlockedByAnotherObject(GridCell cell)
	{
		if (cell == null) return true;

		bool containsSelf = cell.gridObjects?.Contains(parentGridObject) ?? false;
		if (cell.state.HasFlag(Enums.GridCellState.Obstructed))
		{
			// An object's old footprint is allowed during an atomic move/rotation,
			// but original terrain obstruction and any other occupant still block it.
			if (!containsSelf || cell.originalState.HasFlag(Enums.GridCellState.Obstructed))
				return true;
		}

		if (cell.gridObjects == null)
			return false;

		foreach (GridObject gridObject in cell.gridObjects)
		{
			if (gridObject == null || gridObject == parentGridObject ||
			    gridObject is GridCellStateOverride)
				continue;

			if (gridObject.IsActive && !gridObject.scenery)
				return true;
		}

		return false;
	}

	public bool TrySetGridCell(GridCell newAnchor)
	{
		if (newAnchor == null)
		{
			SetGridCell(null);
			return true;
		}

		if (!CanOccupyAt(newAnchor, Direction, out _))
			return false;

		SetGridCell(newAnchor);
		return true;
	}

	public void SetGridCell(GridCell newAnchor)
	{
		ClearOccupation();

		AnchorCell = newAnchor;
		if (AnchorCell == null) return;

		_config ??= GridConfiguration.GetActive();

		var worldCoords = GetWorldCoordinatesAt(
			AnchorCell.GridCoordinates,
			Direction
		);
		bool isWalkThrough = parentGridObject?.gridObjectSettings
			.HasFlag(Enums.GridObjectSettings.CanWalkThrough) ?? false;

		for (int i = 0; i < worldCoords.Count; i++)
		{
			var coord = worldCoords[i];
			var cell = GridSystem.Instance?.GetGridCell(coord);
			if (cell == null) continue;

			OccupiedCells.Add(cell);

			bool isAnchor = coord == AnchorCell.GridCoordinates;
			var newState = cell.state;

			if (!isWalkThrough)
				newState |= Enums.GridCellState.Obstructed;

			if (!isAnchor)
				newState &= ~Enums.GridCellState.Empty;

			cell.AddGridObject(parentGridObject, newState, rebuildConnections: !isWalkThrough);
		}


		EmitSignal(SignalName.PositionChanged, this);
	}

	public void SetDirection(Enums.Direction newDirection)
	{
		if (!TrySetDirection(newDirection))
		{
			GD.PushWarning(
				$"{parentGridObject?.Name ?? Name}: cannot rotate footprint to " +
				$"{newDirection} at {AnchorCell?.GridCoordinates}"
			);
		}
	}

	public bool TrySetDirection(Enums.Direction newDirection)
	{
		if (Direction == newDirection) return true;
		if (newDirection == Enums.Direction.None) return false;

		if (AnchorCell != null && !CanOccupyAt(AnchorCell, newDirection, out _))
			return false;

		Direction = newDirection;
		ApplyDirectionToVisualMesh();

		if (AutoCalculateShape)
			RecalculateShape();
		else if (AnchorCell != null)
			SetGridCell(AnchorCell);

		EmitSignal(SignalName.DirectionChanged, (int)newDirection);
		return true;
	}

	private void ClearOccupation()
	{
		foreach (var cell in OccupiedCells)
		{
			cell?.RestoreOriginalState();
			cell?.RemoveGridObject(parentGridObject, cell.originalState, rebuildConnections: false);
		}

		OccupiedCells.Clear();
	}

	#endregion

	
	#region Direction Utilities

	private void ApplyDirectionToVisualMesh()
	{
		if (Direction == Enums.Direction.None || parentGridObject?.visualMesh == null)
			return;

		var rotation = parentGridObject.visualMesh.Rotation;
		rotation.Y = RotationHelperFunctions.GetRotationRadians(Direction);
		parentGridObject.visualMesh.Rotation = rotation;
	}

	public Enums.Direction GetDirectionFromRotation(float yRadians)
	{
		return RotationHelperFunctions.GetDirectionFromRotation3D(yRadians);
	}

	public static float DirectionToRadians(Enums.Direction dir)
	{
		return RotationHelperFunctions.GetRotationRadians(dir);
	}

	#endregion

	#region Editor Preview

	private void DrawEditorPreview()
	{
		if (AutoCalculateShape && !_shapeCalculated)
			CalculateShapeFromColliders();

		var anchorCoords = _config.WorldToGrid(GlobalPosition);

		Vector3 boundsSize = new Vector3(
			_config.GridSize.X * _config.CellSize.X,
			_config.GridSize.Y * _config.CellSize.Y,
			_config.GridSize.Z * _config.CellSize.Z
		);
		Vector3 boundsCenter = _config.GridWorldOrigin + (boundsSize / 2.0f);
		DebugDraw3D.DrawBox(boundsCenter, Quaternion.Identity, boundsSize, new Color(1, 1, 1, 0.1f), true);

		var worldCoords = GetWorldCoordinatesAt(anchorCoords, Direction);

		foreach (var coord in worldCoords)
		{
			Vector3 worldPos = _config.GridToWorld(coord, cellCenter: true);
			bool isPivot = coord == anchorCoords;
			bool isInBounds = _config.IsValidCoordinate(coord);

			Color color;
			if (!isInBounds)
				color = new Color(1, 0, 0, 0.4f);
			else if (isPivot)
				color = _pivotColor;
			else
				color = _occupiedColor;

			Vector3 boxSize = _config.CellSize * 0.9f;
			DebugDraw3D.DrawBox(worldPos, Quaternion.Identity, boxSize, color, true);
		}

		Vector3 anchorWorld = _config.GridToWorld(anchorCoords, true);
		Vector3 forward = Direction.GetAbsoluteDirectionVector() * _config.CellSize.X;
		DebugDraw3D.DrawArrow(anchorWorld, anchorWorld + forward, Colors.Blue, 0.1f, true);
	}

	#endregion

	#region Validation

	public bool CanPlaceAt(Vector3I anchorCoords)
	{
		GridCell anchorCell = GridSystem.Instance?.GetGridCell(anchorCoords);
		return CanOccupyAt(anchorCell, Direction, out _);
	}

	public List<Vector3I> GetInvalidCells(Vector3I anchorCoords)
	{
		var invalid = new List<Vector3I>();
		Enums.Direction shapeDirection = !AutoCalculateShape && RotateShapeWithDirection
			? Direction
			: Enums.Direction.None;
		var worldCoords = Shape.GetWorldCoordinates(anchorCoords, shapeDirection);
		var supportCoordinates = new HashSet<Vector3I>(
			Shape.GetSupportWorldCoordinates(anchorCoords, shapeDirection)
		);

		foreach (var coord in worldCoords)
		{
			var cell = GridSystem.Instance?.GetGridCell(coord);
			if (cell == null ||
			    cell.state.HasFlag(Enums.GridCellState.Disabled) ||
			    IsBlockedByAnotherObject(cell) ||
			    (supportCoordinates.Contains(coord) &&
			     !cell.state.HasFlag(Enums.GridCellState.Ground)))
			{
				invalid.Add(coord);
			}
		}

		return invalid;
	}

	#endregion

	#region Save/Load

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>();

		data["AutoCalculateShape"] = AutoCalculateShape;
		data["RotateShapeWithDirection"] = RotateShapeWithDirection;
		data["RecursiveShapeDetection"] = RecursiveShapeDetection;
		data["Direction"] = (int)Direction;

		if (AnchorCell != null)
		{
			data["HasPosition"] = true;
			data["AnchorX"] = AnchorCell.GridCoordinates.X;
			data["AnchorY"] = AnchorCell.GridCoordinates.Y;
			data["AnchorZ"] = AnchorCell.GridCoordinates.Z;
		}
		else
		{
			data["HasPosition"] = false;
		}

		if (!AutoCalculateShape && Shape != null && !string.IsNullOrEmpty(Shape.ResourcePath))
			data["ShapePath"] = Shape.ResourcePath;

		return data;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data.TryGetValue("AutoCalculateShape", out var autoCalcVar))
			AutoCalculateShape = autoCalcVar.AsBool();

		if (data.TryGetValue("RotateShapeWithDirection", out var rotateShapeVar))
			RotateShapeWithDirection = rotateShapeVar.AsBool();

		if (data.TryGetValue("RecursiveShapeDetection", out var recursiveVar))
			RecursiveShapeDetection = recursiveVar.AsBool();

		if (data.TryGetValue("Direction", out var dirVar))
			Direction = (Enums.Direction)dirVar.AsInt32();

		if (!AutoCalculateShape && data.TryGetValue("ShapePath", out var pathVar))
		{
			var loaded = GD.Load<GridShape>(pathVar.AsString());
			if (loaded != null) Shape = loaded;
		}

		if (data.TryGetValue("HasPosition", out var hasPosVar) && hasPosVar.AsBool())
		{
			int x = data["AnchorX"].AsInt32();
			int y = data["AnchorY"].AsInt32();
			int z = data["AnchorZ"].AsInt32();

			if (GridSystem.Instance != null)
			{
				var cell = GridSystem.Instance.GetGridCell(new Vector3I(x, y, z));
				if (cell != null) SetGridCell(cell);
			}
		}
	}

	#endregion
}
