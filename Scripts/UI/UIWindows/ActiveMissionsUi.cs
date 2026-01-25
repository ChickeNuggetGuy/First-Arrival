using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot.Collections;

public partial class ActiveMissionsUi : UIWindow
{
	[Export] private Control activeMissionHolder;
	[Export] private PackedScene activeMissionButton;
	private GlobeMissionManager missionManager;

	protected override Task _Setup()
	{
		if (activeMissionHolder == null)
		{
			GD.PrintErr("ActiveMissionsUi.Setup(): activeMissionHolder is null");
			return Task.CompletedTask;
		}

		if (activeMissionButton == null)
		{
			GD.PrintErr("ActiveMissionsUi.Setup(): activeMissionButton is null");
			return Task.CompletedTask;
		}
		
		missionManager = GlobeMissionManager.Instance;

		if (missionManager == null)
		{
			GD.PrintErr("ActiveMissionsUi.Setup(): missionManager is null");
			return Task.CompletedTask;
		}
		//Setup Mission signal listeners
		missionManager.MissionSpawned += MissionManagerOnMissionSpawned;
		missionManager.MissionCompleted += MissionManagerOnMissionCompleted;
		
		//Update any existing Active Missions 
		UpdateActiveMissionButtons();
		return base._Setup();
		
	}

	private void MissionManagerOnMissionCompleted()
	{
		UpdateActiveMissionButtons();
	}

	private void MissionManagerOnMissionSpawned(MissionBase mission)
	{
		CreateActiveMissionButton(mission, activeMissionHolder.GetChildren().Count + 1);
	}


	private void CreateActiveMissionButton(MissionBase mission, int index)
	{
		if (activeMissionButton == null)
		{
			GD.PrintErr("ActiveMissionsUi.CreateActiveMissionButton(): activeMissionButton is null");
			return;
		}

		MissionButtonUI missionButtonUi = activeMissionButton.Instantiate() as MissionButtonUI;


		if (missionButtonUi == null)
		{
			GD.PrintErr("ActiveMissionsUi.CreateActiveMissionButton(): activeMissionButton is null");
			return;
		}
		missionButtonUi.mission = mission;
		missionButtonUi.listIndex = index;
		missionButtonUi.SetupCall();
		activeMissionHolder.AddChild(missionButtonUi);
	}


	private void UpdateActiveMissionButtons()
	{
		ClearActivemissionButtons();
		if (missionManager == null)
		{
			GD.PrintErr("UpdateActiveMissionButtons(): missionManager is null");
			return;
		}
		
		System.Collections.Generic.Dictionary<int, MissionCellDefinition> activeMissions = missionManager.GetActiveMissions();
		if (activeMissions == null || activeMissions.Count == 0)
		{
			GD.Print("UpdateActiveMissionButtons(): activeMissions is null");
			return;
		}

		int index = 0;
		foreach (var missionKVP in activeMissions)
		{
			index++;
			CreateActiveMissionButton(missionKVP.Value.mission, index);
		}
	}


	private void ClearActivemissionButtons()
	{
		if (activeMissionHolder == null)
		{
			GD.PrintErr("ActiveMissionsUi.ClearActivemissionButtons(): activeMissionHolder is null");
			return;
		}
		
		var children = activeMissionHolder.GetChildren();
		if (children == null  || children.Count == 0)
		{
			return;
		}

		foreach (var child in children)
		{
			child.QueueFree();
		}
		
		
	}
}
