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


	public MeshInstance3D visual { get;protected set; }
	
	public int Index { get; private set; }
	
	public void Setup(int index, int homeBaseIndex, TeamBaseCellDefinition baseDefinition)
	{
		Index = index;
		HomeBaseIndex = homeBaseIndex;
		CurrentCellIndex = homeBaseIndex;
		baseCellDefinition = baseDefinition;
	}


	#region Get/Set Functions

	public TeamBaseCellDefinition GetBaseCellDefinition() => baseCellDefinition;
	public void SetBaseCellDefinition(TeamBaseCellDefinition definition) => baseCellDefinition = definition;
	
	public MeshInstance3D GetVisual() => visual;
	public void SetVisual(MeshInstance3D instance) => visual = instance;

	#endregion

}
