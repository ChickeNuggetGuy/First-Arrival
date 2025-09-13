using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;

namespace FirstArrival.Scripts.Managers
{
    [GlobalClass]
    public partial class TurnManager : Manager<TurnManager>
    {
        [Export]
        protected Turn[] turns = new Turn[0];

        private int _currentTurnIndex = 0;

        public Turn CurrentTurn
        {
            get
            {
                if (turns == null || turns.Length == 0)
                {
                    GD.PushWarning("TurnManager: 'turns' array is null or empty.");
                    return null;
                }
                int clampedIndex = Mathf.Clamp(_currentTurnIndex, 0, turns.Length - 1);
                return turns[clampedIndex];
            }
        }

        [Signal]
        public delegate void TurnStartedEventHandler(Turn currentTurn);

        protected override async Task _Setup()
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
        }

        protected override async Task _Execute()
        {
            if (CurrentTurn == null)
            {
                GD.PushWarning("TurnManager: No valid current turn to execute.");
                SetIsBusy(false);
                return;
            }

            GD.Print("---> Executing Turn: ", CurrentTurn?.ResourceName ?? "NULL");

            SetIsBusy(true);

            try
            {
                await CurrentTurn.ExecuteCall();
            }
            catch (Exception ex)
            {
                GD.PushError($"TurnManager: Error during '{CurrentTurn?.ResourceName}' execution: {ex.Message}");
            }
        }

        public void RequestEndOfTurn()
        {
            GD.Print("---> Requesting End of Turn");
            CallDeferred("EndTurn");
        }

        private void EndTurn()
        {
            GD.Print("---> Ending Turn");
            ChangeCurrentTurn();
            SetIsBusy(false);
        }

        private void SetCurrentTurn(int turnIndex)
        {
            if (turns == null || turns.Length == 0)
            {
                GD.PushWarning("TurnManager: Cannot set current turn, 'turns' array is empty.");
                _currentTurnIndex = 0;
                return;
            }

            _currentTurnIndex = Mathf.Clamp(turnIndex, 0, turns.Length - 1);
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
            return (_currentTurnIndex + 1) % turns.Length;
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (!IsBusy)
            {
                _ = _Execute();
            }
        }
    }
}