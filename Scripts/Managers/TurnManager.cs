using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class TurnManager : Manager<TurnManager>
{
	[Export] protected Turn[] turns = new Turn[0];

	public int CurrentTurnIndex { get; protected set; } = 0;

	public Turn CurrentTurn
	{
		get
		{
			if (turns == null || turns.Length == 0)
			{
				GD.PushWarning("TurnManager: 'turns' array is null or empty.");
				return null;
			}

			int clampedIndex = Mathf.Clamp(CurrentTurnIndex, 0, turns.Length - 1);
			return turns[clampedIndex];
		}
	}

	[Signal]
	public delegate void TurnStartedEventHandler(Turn currentTurn);

	public override string GetManagerName()=> "TurnManager";

	protected override async Task _Setup(bool loadingData)
	{
		if (turns == null || turns.Length == 0)
		{
			GD.PushWarning("TurnManager: No turns defined. Setup complete.");
			SetCurrentTurn(0);
			return;
		}

		foreach (var turn in turns)
		{
			if (turn != null)
			{
				await turn.SetupCall();
			}
			else
			{
				GD.PushWarning("TurnManager: Encountered a null turn during setup.");
			}
		}

		SetCurrentTurn(0);
		if (GameManager.Instance != null)
		{
			TurnStarted += GameManager.Instance.CheckGameState;
		}
		else 
		{
			GD.PushError("TurnManager: GameManager Instance is null during setup!");
		}
	}
	
	public override void Deinitialize()
	{
		TurnStarted -= GameManager.Instance.CheckGameState;
		return;
	}

	protected override async Task _Execute(bool loadingData)
	{
		if (CurrentTurn == null)
		{
			GD.PushWarning("TurnManager: No valid current turn to execute.");
			SetIsBusy(false);
			return;
		}

		GD.Print("---> Executing Turn: ", CurrentTurn?.ResourceName ?? "NULL");
		
		try
		{
			if (CurrentTurn != null) await CurrentTurn.ExecuteCall();
		}
		catch (Exception ex)
		{
			GD.PushError($"TurnManager: Error during '{CurrentTurn?.ResourceName}' execution: {ex.Message}");
		}
		return;
	}

	public void RequestEndOfTurn()
	{
		CallDeferred("EndTurn");
	}

	private void EndTurn()
	{
		GD.Print("---> Ending Turn");
		ActionManager.Instance?.ProcessDelayedActions();
		ChangeCurrentTurn();
		SetIsBusy(false);
	}

	private void SetCurrentTurn(int turnIndex)
	{
		if (turns == null || turns.Length == 0)
		{
			GD.PushWarning("TurnManager: Cannot set current turn, 'turns' array is empty.");
			CurrentTurnIndex = 0;
			return;
		}

		CurrentTurnIndex = Mathf.Clamp(turnIndex, 0, turns.Length - 1);
		EmitSignal(SignalName.TurnStarted, CurrentTurn);

		GD.Print("<--- Turn Started: ", CurrentTurn?.ResourceName ?? "NULL");
	}

	private void ChangeCurrentTurn()
	{
		if (turns == null || turns.Length == 0)
		{
			GD.PushWarning("TurnManager: Cannot change turn, 'turns' array is empty.");
			SetCurrentTurn(0);
			return;
		}

		int nextIndex = GetNextTurnIndex();
		SetCurrentTurn(nextIndex);
	}

	private int GetNextTurnIndex()
	{
		if (turns == null || turns.Length <= 1)
		{
			return 0;
		}

		for (int i = 1; i <= turns.Length; i++)
		{
			int nextIndex = (CurrentTurnIndex + i) % turns.Length;

			var turn = turns[nextIndex];
			if (turn != null && (turn.repeatable || turn.timesExectuted == 0))
			{
				return nextIndex;
			}
		}

		// Fallback: Start from beginning again.
		return 0;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);


		if (!IsBusy)
		{
			SetIsBusy(true);
			_ = _Execute(false);
		}
	}

	public override void _ExitTree()
	{
		TurnStarted -= GameManager.Instance.CheckGameState;
		base._ExitTree();
		
	}


	#region Manager Data

	public override void Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		base.Load(data);
		if(!HasLoadedData) return;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		return null;
	}

	#endregion
}