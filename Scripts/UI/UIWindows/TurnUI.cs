using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;

[GlobalClass]
public partial class TurnUI : UIWindow
{
	[Export] private Label currentTurnLabel;
	[Export] private Button endTurnButton;

	protected override Task _Setup()
	{
		if (endTurnButton != null)
		{
			endTurnButton.Pressed -= EndTurnButtonOnPressed;
			endTurnButton.Pressed += EndTurnButtonOnPressed;
		}

		if (TurnManager.Instance != null)
		{
			TurnManager.Instance.TurnStarted -= InstanceOnTurnStarted;
			TurnManager.Instance.TurnStarted += InstanceOnTurnStarted;
		}

		return base._Setup();
	}

	public override void _ExitTree()
	{
		base._ExitTree();

		if (endTurnButton != null)
		{
			endTurnButton.Pressed -= EndTurnButtonOnPressed;
		}

		if (TurnManager.Instance != null)
		{
			TurnManager.Instance.TurnStarted -= InstanceOnTurnStarted;
		}
	}

	private void EndTurnButtonOnPressed()
	{
		TurnManager.Instance?.RequestEndOfTurn();
	}

	private void InstanceOnTurnStarted(Turn currentTurn)
	{
		UpdateTurnUI(currentTurn);
	}

	private void UpdateTurnUI(Turn currentTurn)
	{
		if (currentTurnLabel != null)
		{
			currentTurnLabel.Text = "Current turn: " + currentTurn?.team.ToString() ?? "None";
		}
	}
}