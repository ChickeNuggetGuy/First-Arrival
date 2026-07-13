using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

public partial class MissionCellDefinition : HexCellDefinition
{
	public MissionBase mission;

	public Node3D missionVisual = null;
	
	public Enums.MissionStatus missionStatus = Enums.MissionStatus.None;

	[Export] public Dictionary<Enums.MissionStatus, int> scoreChange = new()
	{
		{ Enums.MissionStatus.None, 0 },
		{ Enums.MissionStatus.Failed, -250 },
		{ Enums.MissionStatus.Successful, 325 },
	};

	public Craft onRouteCraft; 

	public MissionCellDefinition(
		int cellIndex,
		string name,
		MissionBase mission,
		Node3D missionVisual = null,
		Enums.MissionStatus missionStatus = Enums.MissionStatus.None,
		Craft craft = null) : base(cellIndex, name)
	{
		this.mission = mission;
		if (missionVisual != null)
			this.missionVisual = missionVisual;
		this.missionStatus = missionStatus;
		this.onRouteCraft = craft;

	}

	public void SetOnRouteCraft(Craft craft)
	{
		onRouteCraft = craft;
		missionStatus = Enums.MissionStatus.OnRoute;
	}
	
	

	public override Dictionary<string, Variant> Save()
	{
		Dictionary<string, Variant> craftData = new Dictionary<string, Variant>() { };
		if (onRouteCraft != null)
		{
			craftData = onRouteCraft.Save();
		}
		var data = base.Save();
		data.Add("missionData", mission.Save());
		data.Add("missionClass", mission.GetType().Name);
		data.Add("missionStatus", (int)missionStatus);
		data.Add("onRouteCraft", craftData);
		return data;
	}
}