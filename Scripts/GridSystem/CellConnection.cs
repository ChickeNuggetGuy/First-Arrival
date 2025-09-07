
using Godot;

public struct CellConnection
{
	public Vector3I FromGridCellCoords { get; private set; }
	public Vector3I ToGridCellCoords { get; private set; }

	public CellConnection(Vector3I fromFridCell, Vector3I toFridCell)
	{
		this.FromGridCellCoords = fromFridCell;
		this.ToGridCellCoords = toFridCell;
	}
}