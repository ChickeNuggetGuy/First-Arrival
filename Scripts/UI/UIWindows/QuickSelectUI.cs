using Godot;
using System;
using System.Collections.Generic;
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
	private List<QuickSelectButtonUI> quickSelectButtons = new List<QuickSelectButtonUI>();
	
	protected override Task _Setup()
	{
		base._Setup();
		GridObjectTeamHolder playerTeamHolder =
			GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);


		foreach (var gridObject in playerTeamHolder.GridObjects[Enums.GridObjectState.Active])
		{
			if (gridObject != null)
			{
				quickSelectButtons.Add(InstantiateQuickSelectBtoon(gridObject));
			}
		}
		playerTeamHolder.GridObjectListChanged += PlayerTeamHolderOnGridObjectListChanged;
		return base._Setup();
	}

	private void PlayerTeamHolderOnGridObjectListChanged(GridObjectTeamHolder gridObjectTeamHolder)
	{
		GD.Print($"Try Remove Quick select button {quickSelectButtons.Count}");
		for (var index = quickSelectButtons.Count -1; index > 0; index--)
		{
			var button = quickSelectButtons[index];
			if (gridObjectTeamHolder.GridObjects[Enums.GridObjectState.Inactive].Contains(button.TargetGridObject))
			{
				GD.Print("Remove Quick select button");
				RemoveQuickSelectBtoon(button);
			}
			else
			{
				GD.Print("Unit stll active");
			}
		}
	}

	private void RemoveQuickSelectBtoon(QuickSelectButtonUI button)
	{
		quickSelectButtons.Remove(button);
		button.QueueFree();
	}
	private QuickSelectButtonUI InstantiateQuickSelectBtoon(GridObject gridObject)
	{
		QuickSelectButtonUI instantiateButton = quickSelectButtonScene.Instantiate() as  QuickSelectButtonUI;
		quickSelectHolder.AddChild(instantiateButton);
		instantiateButton.SetTargetGridObject(gridObject);
		
		
		return instantiateButton;
	}
}
