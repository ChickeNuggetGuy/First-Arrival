using Godot;
using System.Threading.Tasks;

[GlobalClass]
public partial class SpeedButtonUI : UIElement
{
	[Export] public int timeSpeed = 1;
	[Export] public Button Button;
	private GlobeTimeManager subscribedTimeManager;
	private bool buttonConnected;

	protected override Task _Setup()
	{
		if (Button == null)
			return Task.CompletedTask;

		Button.Text = $"{timeSpeed}x";
		Button.ToggleMode = true;
		if(!Button.IsConnected(BaseButton.SignalName.Pressed, Callable.From(ButtonOnPressed)))
		{
			Button.Pressed += ButtonOnPressed;
			buttonConnected = true;
		}

		subscribedTimeManager = GlobeTimeManager.Instance;
		if (subscribedTimeManager != null)
		{
			subscribedTimeManager.TimeSpeedChanged += OnTimeSpeedChanged;
			OnTimeSpeedChanged(subscribedTimeManager.GetTimeSpeed());
		}

		return Task.CompletedTask;
	}

	public override void _ExitTree()
	{
		if (buttonConnected && Button != null)
			Button.Pressed -= ButtonOnPressed;
		buttonConnected = false;

		if (subscribedTimeManager != null &&
			GodotObject.IsInstanceValid(subscribedTimeManager))
		{
			subscribedTimeManager.TimeSpeedChanged -= OnTimeSpeedChanged;
		}
		subscribedTimeManager = null;

		base._ExitTree();
	}

	private void ButtonOnPressed()
	{
		GlobeTimeManager.Instance?.SetTimeSpeed(timeSpeed);
	}

	private void OnTimeSpeedChanged(int newTimeSpeed)
	{
		if (Button != null)
			Button.ButtonPressed = newTimeSpeed == timeSpeed;
	}
}
