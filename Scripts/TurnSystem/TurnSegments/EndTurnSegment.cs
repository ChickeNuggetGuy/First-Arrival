using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;

[GlobalClass]
public partial class EndTurnSegment : TurnSegment
{
	protected override async Task _Setup()
	{
		return;
	}

	protected override async Task _Execute()
	{
		TurnManager.Instance.RequestEndOfTurn();
	}
}
