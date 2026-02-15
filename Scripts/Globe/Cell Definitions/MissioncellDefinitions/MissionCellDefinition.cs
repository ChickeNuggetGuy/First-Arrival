using Godot;

public partial class MissionCellDefinition : HexCellDefinition
{
	public MissionBase mission;

	public Node3D missionVisual = null;

	public MissionCellDefinition(int cellIndex, string name, MissionBase mission, Node3D missionVisual = null) : base(cellIndex, name)
	{
		this.mission = mission;
		if (missionVisual != null)
			this.missionVisual = missionVisual;
	}
	
	

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = base.Save();
		data.Add("missionData", mission.Save());
		data.Add("missionClass", mission.GetType().Name);
		return data;
	}
}