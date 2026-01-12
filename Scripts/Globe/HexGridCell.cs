using FirstArrival.Scripts.Utility;
using Godot;

public struct HexCellData
{
	public Vector2 LatLon;
	public Vector3 Center;
	public Vector3[] Corners;
	public int Index;
	public Enums.HexGridType cellType;
	public bool IsPentagon;
	
	public int[] Neighbors;
}