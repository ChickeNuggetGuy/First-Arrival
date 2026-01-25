using Godot;
using System;

public partial class HexCellDefinition
{
	public string definitionName;
	public int cellIndex;

	public HexCellDefinition(int cellIndex, string name)
	{
		this.cellIndex = cellIndex;
		this.definitionName = name;
	}

	public virtual Godot.Collections.Dictionary<string, Variant> Save()
	{
		Godot.Collections.Dictionary<string, Variant> returnData = new();
		
		returnData.Add("cellIndex", cellIndex);
		returnData.Add("definitionName", definitionName);
		return returnData;
	}
}
