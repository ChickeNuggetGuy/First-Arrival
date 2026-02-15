using Godot;
using System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using Godot.Collections;

public partial class ExplosiveComponent : ItemComponent
{
	[Export] private bool isActive  = false;
	[Export] private int turnTimer = 2;
	private int _currentTurnTimer = 0;
	private int turnActivated = -1;
	protected override void _setup()
	{
		TurnManager turnManager = TurnManager.Instance;
		if (turnManager == null) return;
		
		turnManager.TurnStarted += TurnmanagerOnTurnStarted;
	}

	private void TurnmanagerOnTurnStarted(Turn currentTurn)
	{
		_currentTurnTimer--;
		if (_currentTurnTimer == 0)
		{
			GD.Print("BOOOOOM!");
		}
	}

	public void Activate(int currentTurn)
	{
		isActive = true;
		_currentTurnTimer = turnTimer;
		turnActivated = currentTurn;
		TurnManager.Instance.TurnStarted += TurnmanagerOnTurnStarted;
	}

	public override Dictionary<string, Callable> GetContextActions()
	{
		TurnManager turnManager = TurnManager.Instance;
		int currentTurnIndex = turnManager.CurrentTurnIndex;
		Dictionary<string, Callable> actions = new Dictionary<string, Callable>();
		actions.Add("Activate", Callable.From((int index) => Activate(currentTurnIndex)));
		return base.GetContextActions();
	}
}
