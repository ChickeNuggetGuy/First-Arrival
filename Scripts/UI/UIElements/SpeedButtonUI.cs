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
		if(!Button.IsConnected(BaseButton.SignalName.Pressed, Callable.From(ButtonOnPressed)))
		Button.Pressed += ButtonOnPressed; 
	}

	public override void _ExitTree()
	{
		Button.Pressed -= ButtonOnPressed; 
		base._ExitTree();
		
	}

	private void ButtonOnPressed()
	{
		GlobeTimeManager.Instance.SetTimeSpeed(timeSpeed);
	}
}
