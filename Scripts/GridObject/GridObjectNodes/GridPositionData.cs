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

		Direction = GetDirectionFromRotation(GlobalRotation.Y);

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
		// Ensure config is fresh at runtime
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

	public void SetGridCell(GridCell newAnchor)
	{
		ClearOccupation();

		AnchorCell = newAnchor;
		if (AnchorCell == null) return;

		_config ??= GridConfiguration.GetActive();

		var worldCoords = Shape.GetWorldCoordinates(AnchorCell.GridCoordinates);
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
		if (Direction == newDirection) return;

		Direction = newDirection;

		if (AutoCalculateShape)
			RecalculateShape();
		else if (AnchorCell != null)
			SetGridCell(AnchorCell);

		EmitSignal(SignalName.DirectionChanged, (int)newDirection);
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

	public Enums.Direction GetDirectionFromRotation(float yRadians)
	{
		float angle = Mathf.PosMod(yRadians, Mathf.Tau);
		float deg = Mathf.RadToDeg(angle);

		if (deg < 45 || deg >= 315) return Enums.Direction.South;
		if (deg < 135) return Enums.Direction.West;
		if (deg < 225) return Enums.Direction.North;
		return Enums.Direction.East;
	}

	public static float DirectionToRadians(Enums.Direction dir)
	{
		return dir switch
		{
			Enums.Direction.South => 0f,
			Enums.Direction.West => Mathf.Pi * 0.5f,
			Enums.Direction.North => Mathf.Pi,
			Enums.Direction.East => Mathf.Pi * 1.5f,
			_ => 0f
		};
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

		var worldCoords = Shape.GetWorldCoordinates(anchorCoords);

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
		if (GridSystem.Instance == null) return false;

		var worldCoords = Shape.GetWorldCoordinates(anchorCoords);

		foreach (var coord in worldCoords)
		{
			var cell = GridSystem.Instance.GetGridCell(coord);
			if (cell == null) return false;
			if (cell.state.HasFlag(Enums.GridCellState.Obstructed)) return false;
			if (!cell.state.HasFlag(Enums.GridCellState.Ground)) return false;
		}

		return true;
	}

	public List<Vector3I> GetInvalidCells(Vector3I anchorCoords)
	{
		var invalid = new List<Vector3I>();
		var worldCoords = Shape.GetWorldCoordinates(anchorCoords);

		foreach (var coord in worldCoords)
		{
			var cell = GridSystem.Instance?.GetGridCell(coord);
			if (cell == null ||
			    cell.state.HasFlag(Enums.GridCellState.Obstructed) ||
			    !cell.state.HasFlag(Enums.GridCellState.Ground))
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