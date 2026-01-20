using Godot;
using System;
using System.Threading.Tasks;

[GlobalClass]
public partial class SpeedButtonUI : UIElement
{
	[Export] public int timeSpeed = 1;
	[Export] public Button Button;
	protected override async Task _Setup()
	{
		if (Button == null)
			return;
		
		Button.Text = timeSpeed.ToString() + "x";
		Button.Pressed += ButtonOnPressed; 
	}

	private void ButtonOnPressed()
	{
		GlobeTimeManager.Instance.SetTimeSpeed(timeSpeed);
	}
}
