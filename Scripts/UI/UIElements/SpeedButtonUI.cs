using Godot;
using System.Threading.Tasks;

[GlobalClass]
public partial class SpeedButtonUI : UIElement
{
	/// <summary>Game seconds advanced per real second when selected.</summary>
	[Export(PropertyHint.Range, "0,86400,1,or_greater")] public int timeSpeed = 1;
	[Export] public Button Button;
	private GlobeTimeManager subscribedTimeManager;
	private bool buttonConnected;

	protected override Task _Setup()
	{
		if (Button == null)
			return Task.CompletedTask;

		Button.Text = FormatDuration(timeSpeed);
		Button.TooltipText = timeSpeed <= 0
			? "Pause globe time"
			: $"Advance {Button.Text} of game time per real second";
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

	private static string FormatDuration(int seconds)
	{
		if (seconds <= 0) return "Pause";
		if (seconds % 86400 == 0)
			return $"{seconds / 86400} {(seconds == 86400 ? "day" : "days")}";
		if (seconds % 3600 == 0)
			return $"{seconds / 3600} {(seconds == 3600 ? "hour" : "hours")}";
		if (seconds % 60 == 0) return $"{seconds / 60} min";
		return $"{seconds} sec";
	}

	private void ButtonOnPressed()
	{
		GlobeTimeManager timeManager = GlobeTimeManager.Instance;
		if (timeManager == null) return;
		timeManager.SetTimeSpeed(timeSpeed);
		OnTimeSpeedChanged(timeManager.GetTimeSpeed());
	}

	private void OnTimeSpeedChanged(int newTimeSpeed)
	{
		if (Button != null)
			Button.SetPressedNoSignal(newTimeSpeed == timeSpeed);
	}
}
