using Godot;
using System;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Utility;

[Tool]
[GlobalClass]
public partial class Craft : ItemData
{
    [Export] public int MaxSpeed { get; set; }
    [Export] public int Acceleration { get; set; }
    [Export] public bool IsAvailable { get; set; }
    
    [Export] public Enums.CraftStatus Status { get; set; } = Enums.CraftStatus.Home;
    
    [Export] public int CurrentCellIndex { get; set; } = -1;
    [Export] public int HomeBaseIndex { get; set; } = -1;
    protected TeamBaseCellDefinition baseCellDefinition;
    [Export] public int TargetCellIndex { get; set; } = -1;

    public MeshInstance3D visual { get; protected set; }
    public int Index { get; private set; }

    public void Setup(int index, int homeBaseIndex, TeamBaseCellDefinition baseDefinition)
    {
	    Index = index;
	    
	    if (HomeBaseIndex == -1)
		    HomeBaseIndex = homeBaseIndex;

	    if (CurrentCellIndex == -1)
		    CurrentCellIndex = homeBaseIndex;

	    baseCellDefinition = baseDefinition;
    }

    #region Save / Load

    public Godot.Collections.Dictionary<string, Variant> Save()
    {
        return new Godot.Collections.Dictionary<string, Variant>
        {
            { "itemID", this.ItemID },
            { "index", Index },
            { "status", (int)Status },
            { "currentCellIndex", CurrentCellIndex },
            { "targetCellIndex", TargetCellIndex },
            { "homeBaseIndex", HomeBaseIndex },
            { "maxSpeed", MaxSpeed },
            { "acceleration", Acceleration },

        };
    }

    public void Load(Godot.Collections.Dictionary<string, Variant> data)
    {
	    if (data.ContainsKey("index")) Index = data["index"].AsInt32();
	    if (data.ContainsKey("status")) Status = (Enums.CraftStatus)data["status"].AsInt32();
	    if (data.ContainsKey("currentCellIndex")) CurrentCellIndex = data["currentCellIndex"].AsInt32();
	    if (data.ContainsKey("targetCellIndex")) TargetCellIndex = data["targetCellIndex"].AsInt32();
	    if (data.ContainsKey("homeBaseIndex")) HomeBaseIndex = data["homeBaseIndex"].AsInt32();
	    if (data.ContainsKey("maxSpeed")) MaxSpeed = data["maxSpeed"].AsInt32();
	    if (data.ContainsKey("acceleration")) Acceleration = data["acceleration"].AsInt32();
    }

    #endregion

    #region Get/Set Functions
    public TeamBaseCellDefinition GetBaseCellDefinition() => baseCellDefinition;
    public void SetBaseCellDefinition(TeamBaseCellDefinition definition) => baseCellDefinition = definition;
    public MeshInstance3D GetVisual() => visual;
    public void SetVisual(MeshInstance3D instance) => visual = instance;
    #endregion
}