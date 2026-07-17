using System.Collections.Generic;
using FirstArrival.Scripts.Utility;
using Godot;

/// <summary>
/// Draws a collection of grid cells as one batched, shader-driven overlay.
/// </summary>
public partial class GridCellHighlighter : Node3D
{
	private const string ShaderPath = "res://Shaders/grid_cell_highlight.gdshader";

	[Export] public Color HighlightColor { get; set; } = new(0.15f, 0.75f, 1.0f, 0.38f);
	[Export] public Color BorderColor { get; set; } = new(0.55f, 0.95f, 1.0f, 0.9f);
	[Export(PropertyHint.Range, "0.0,0.25,0.005")]
	public float HeightOffset { get; set; } = 0.06f;
	[Export(PropertyHint.Range, "0.1,1.0,0.01")]
	public float CellScale { get; set; } = 0.92f;

	private MultiMeshInstance3D _instance;
	private MultiMesh _multiMesh;
	private QuadMesh _quad;
	private ShaderMaterial _material;

	public override void _Ready()
	{
		_quad = new QuadMesh();
		_material = new ShaderMaterial
		{
			Shader = GD.Load<Shader>(ShaderPath)
		};
		_material.SetShaderParameter("highlight_color", HighlightColor);
		_material.SetShaderParameter("border_color", BorderColor);
		_quad.Material = _material;

		_multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			Mesh = _quad,
			InstanceCount = 0
		};

		_instance = new MultiMeshInstance3D
		{
			Name = "CellHighlightInstances",
			Multimesh = _multiMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_instance);
	}

	public void ShowCells(IEnumerable<GridCell> cells)
	{
		if (_multiMesh == null || cells == null)
		{
			Clear();
			return;
		}

		var validCells = new List<GridCell>();
		foreach (GridCell cell in cells)
		{
			if (
				cell != null
				&& cell != GridCell.Null
				&& cell.fogState != Enums.FogState.Unseen
			)
				validCells.Add(cell);
		}

		Vector2 gridCellSize = FirstArrival.Scripts.Managers.GridSystem.Instance?.CellSize
			?? Vector2.One;
		_quad.Size = new Vector2(
			gridCellSize.X * CellScale,
			gridCellSize.X * CellScale
		);

		_multiMesh.InstanceCount = validCells.Count;
		var flatOnGrid = new Basis(Vector3.Right, -Mathf.Pi / 2.0f);
		for (int i = 0; i < validCells.Count; i++)
		{
			Vector3 position = validCells[i].WorldCenter + Vector3.Up * HeightOffset;
			_multiMesh.SetInstanceTransform(i, new Transform3D(flatOnGrid, position));
		}

		Visible = validCells.Count > 0;
	}

	public void Clear()
	{
		if (_multiMesh != null)
			_multiMesh.InstanceCount = 0;
		Visible = false;
	}
}
