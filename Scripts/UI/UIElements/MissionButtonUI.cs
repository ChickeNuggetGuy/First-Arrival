using Godot;
using System;
using System.Threading.Tasks;

[GlobalClass]
public partial class MissionButtonUI : UIElement
{
	[Export] private Button Button;
	public MissionBase mission;
	public int listIndex = -1;
	protected override async Task _Setup()
	{
		if (Button == null)
		{
			GD.PrintErr("MissionButtonUI.Setup(): Button is null");
			return;
		}

		Button.Text = listIndex.ToString();
		
		if(!Button.IsConnected(Button.SignalName.Pressed, Callable.From(ButtonOnPressed)))
			Button.Pressed += ButtonOnPressed;
	}


	private void ButtonOnPressed()
	{
		GD.Print($"Button.OnPressed(): {mission.cellIndex}");
		OrbitalCamera.Instance.FocusOnCell(mission.cellIndex);
	}
}
