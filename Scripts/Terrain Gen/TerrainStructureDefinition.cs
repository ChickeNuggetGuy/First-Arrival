using Godot;

/// <summary>
/// Describes a structure that can be placed on procedural terrain.
/// The terrain footprint uses the same cell and pivot conventions as GridShape,
/// but it may be larger than a structure's separate gameplay footprint.
/// </summary>
[GlobalClass]
public partial class TerrainStructureDefinition : Resource
{
	public enum LocationMode
	{
		Random,
		FixedAnchor
	}

	public enum TerrainInteraction
	{
		/// <summary>
		/// Only place the structure when the existing terrain is already flat enough.
		/// </summary>
		FitExistingTerrain,

		/// <summary>
		/// Flatten the footprint and blend the surrounding terrain into it.
		/// </summary>
		FlattenAndBlend
	}

	[Export] public bool Enabled { get; set; } = true;

	[ExportGroup("Structure")]
	[Export] public PackedScene StructureScene { get; set; }

	[Export(PropertyHint.ResourceType, "GridShape")]
	public GridShape Footprint { get; set; }

	[Export(PropertyHint.Range, "0,100,1")]
	public int SpawnCount { get; set; } = 1;

	[Export]
	public bool ApplyFootprintToGridPositionData { get; set; } = false;

	[ExportGroup("Terrain Interaction")]
	[Export]
	public TerrainInteraction Interaction { get; set; } =
		TerrainInteraction.FlattenAndBlend;

	[Export(PropertyHint.Range, "0,20,0.1")]
	public float MaxHeightDifference { get; set; } = 0.5f;

	[Export(PropertyHint.Range, "0,32,1")]
	public int BlendRadiusCells { get; set; } = 3;

	[Export(PropertyHint.Range, "0.1,8,0.1")]
	public float BlendExponent { get; set; } = 1.0f;

	[Export(PropertyHint.Range, "-20,20,0.05")]
	public float HeightOffset { get; set; } = 0.0f;

	[ExportGroup("Placement")]
	[Export] public LocationMode Location { get; set; } = LocationMode.Random;
	[Export] public Vector2I FixedAnchorCell { get; set; } = Vector2I.Zero;
	[Export] public bool AvoidManMadeChunks { get; set; } = true;
	[Export] public bool AllowQuarterTurns { get; set; } = false;

	[Export(PropertyHint.Range, "0,32,1")]
	public int EdgePaddingCells { get; set; } = 1;

	[Export(PropertyHint.Range, "0,32,1")]
	public int SeparationCells { get; set; } = 1;

	[Export(PropertyHint.Range, "0,16,1")]
	public int GrassClearanceCells { get; set; } = 0;

	[Export(PropertyHint.Range, "1,1000,1")]
	public int AttemptsPerInstance { get; set; } = 100;
}
