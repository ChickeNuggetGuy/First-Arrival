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
	
	protected override async Task _Setup()
	{
		await base._Setup();

		GridObjectTeamHolder playerTeamHolder =
			GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);

		if (playerTeamHolder == null)
		{
			GD.PrintErr("QuickSelectUI: playerTeamHolder is null!");
			return;
		}

		UpdateButtons(playerTeamHolder);
		playerTeamHolder.GridObjectListChanged += UpdateButtons;
	}

	private void UpdateButtons(GridObjectTeamHolder gridObjectTeamHolder)
	{
		// Remove buttons for units that are no longer active
		for (var index = quickSelectButtons.Count - 1; index >= 0; index--)
		{
			var button = quickSelectButtons[index];
			if (button == null || !IsInstanceValid(button) || !gridObjectTeamHolder.GridObjects[Enums.GridObjectState.Active].Contains(button.TargetGridObject))
			{
				RemoveQuickSelectButonn(button);
			}
		}

		// Add buttons for active units that don't have one yet
		foreach (var gridObject in gridObjectTeamHolder.GridObjects[Enums.GridObjectState.Active])
		{
			if (gridObject == null) continue;
			
			if (quickSelectButtons.All(b => b.TargetGridObject != gridObject))
			{
				quickSelectButtons.Add(InstantiateQuickSelectBtoon(gridObject));
			}
		}
	}

	private void RemoveQuickSelectButonn(QuickSelectButtonUI button)
	{
		if (button == null) return;
		quickSelectButtons.Remove(button);
		if (IsInstanceValid(button))
		{
			button.QueueFree();
		}
	}
	private QuickSelectButtonUI InstantiateQuickSelectBtoon(GridObject gridObject)
	{
		QuickSelectButtonUI instantiateButton = quickSelectButtonScene.Instantiate() as  QuickSelectButtonUI;
		instantiateButton.SetupCall();
		quickSelectHolder.AddChild(instantiateButton);
		instantiateButton.SetTargetGridObject(gridObject);
		
		
		return instantiateButton;
	}
}
