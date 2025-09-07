using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Array = Godot.Collections.Array;

[GlobalClass]
public partial class QuickSelectUI : UIWindow
{

	[Export] private Control quickSelectHolder;
	[Export] private PackedScene quickSelectButtonScene;
	private Array quickSelectButtons=new Array();
	protected override Task _Setup()
	{
		base._Setup();

		foreach (var gridObject in GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).GridObjects[Enums.GridObjectState.Active])
		{
			if (gridObject != null)
			{
				quickSelectButtons.Append(InstantiateQuickSelectBtoon(gridObject));
			}
		}
		return base._Setup();
	}

	private QuickSelectButtonUI InstantiateQuickSelectBtoon(GridObject gridObject)
	{
		QuickSelectButtonUI instantiateButton = quickSelectButtonScene.Instantiate() as  QuickSelectButtonUI;
		quickSelectHolder.AddChild(instantiateButton);
		instantiateButton.SetTargetGridObject(gridObject);
		
		
		return instantiateButton;
	}
}
