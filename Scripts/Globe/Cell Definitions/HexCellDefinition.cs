using Godot;
using System;

public partial class HexCellDefinition
{
	public int cellIndex;

	public HexCellDefinition(int cellIndex)
	{
		this.cellIndex = cellIndex;
	}

	public virtual Godot.Collections.Dictionary<string, Variant> Save()
	{
		Godot.Collections.Dictionary<string, Variant> returnData = new();
		
		returnData.Add("cellIndex", cellIndex);
		return returnData;
	}
}
