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

	public virtual void Load(
		Godot.Collections.Dictionary<string, Variant> data
	)
	{
		if (data.ContainsKey("cellIndex"))
		{
			cellIndex = data["cellIndex"].AsInt32();
		}

		if (data.ContainsKey("definitionName"))
		{
			definitionName = data["definitionName"].AsString();
		}
	}
	
}
