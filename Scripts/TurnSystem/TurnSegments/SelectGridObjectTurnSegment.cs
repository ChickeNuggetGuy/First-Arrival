using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class SelectGridObjectTurnSegment : TurnSegment
{
	public enum SelectGridObjectType
	{
		Random,
		First,
		Last
	}
	
	[Export] public SelectGridObjectType selectType = SelectGridObjectType.First;

	protected override async Task _Setup()
	{
		return;
	}

	protected override async Task _Execute()
	{
		GridObjectTeamHolder teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(parentTurn.team);
		var activeObjects = teamHolder.GridObjects[Enums.GridObjectState.Active];

		if (activeObjects.Count == 0) 
		{
			GD.PushWarning("SelectGridObjectSegment: No active objects to select.");
			return;
		}

		GridObject gridObjectToSelect = null;
		switch (selectType)
		{
			case SelectGridObjectType.First:
				gridObjectToSelect = activeObjects[0];
				break;
			case SelectGridObjectType.Last:
				gridObjectToSelect = activeObjects[activeObjects.Count - 1];
				break;
			case SelectGridObjectType.Random:
				gridObjectToSelect = activeObjects[GD.RandRange(0, activeObjects.Count - 1)];
				break;
		}
		teamHolder.SetSelectedGridObject(gridObjectToSelect);
	}
}
