using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class ChunkData : Resource
{
	[Export]
	public Vector2I chunkCoordinates { get; set; }

	public enum ChunkType
	{
		Procedural,
		ManMade
	}

	[Export]
	public ChunkType chunkType { get; set; } = ChunkType.Procedural;

	// Name/id used to build prefab path:
	// res://Scenes/Chunks/{chunkGOIndex}.tscn
	// Leave empty if not used.
	[Export]
	public string chunkGOIndex { get; set; } = "";

	// Non-serialized fields
	public Chunk chunk;
	private Node3D chunkNode;

	public ChunkData() { }

	public ChunkData(
		Vector2I coords,
		ChunkType type,
		Node3D chunkNode = null,
		string prefabId = ""
	)
	{
		chunkCoordinates = coords;
		chunkType = type;
		this.chunkNode = chunkNode;
		chunkGOIndex = prefabId;

		if (chunkNode != null)
			chunk = chunkNode.GetOrCreateChildOfType<Chunk>();
	}

	public Node3D GetChunkNode() => chunkNode;
	public void SetChunkNode(Node3D value) => chunkNode = value;
	public ChunkType GetChunkType() => chunkType;

	// Return the inspector-provided id (no hard-coded default).
	public string GetchunkGOIndex() => chunkGOIndex ?? "";
}