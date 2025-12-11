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

		GridObject gridObjectToSelect = null;
		switch (selectType)
		{
			case SelectGridObjectType.First:
				gridObjectToSelect = teamHolder.GridObjects[Enums.GridObjectState.Active][0];
				break;
			case SelectGridObjectType.Last:
				gridObjectToSelect = teamHolder.GridObjects[Enums.GridObjectState.Active][-1];
				break;
			case SelectGridObjectType.Random:
				gridObjectToSelect = teamHolder.GridObjects[Enums.GridObjectState.Active][GD.RandRange(0, teamHolder.GridObjects[Enums.GridObjectState.Active].Count)];
				break;
		}
		teamHolder.SetSelectedGridObject(gridObjectToSelect);
	}
}
