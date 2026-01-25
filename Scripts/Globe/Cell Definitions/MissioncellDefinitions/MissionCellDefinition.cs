using Godot;

public partial class MissionCellDefinition : HexCellDefinition
{
	public MissionBase mission;

	public MissionCellDefinition(int cellIndex, string name, MissionBase mission) : base(cellIndex, name)
	{
		this.mission = mission;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = base.Save();
		data.Add("missionData", mission.Save());
		data.Add("missionClass", mission.GetType().Name);
		return data;
	}
}