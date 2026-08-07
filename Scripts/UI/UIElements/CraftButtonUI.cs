using Godot;
using System.Threading.Tasks;

public partial class CraftButtonUI : UIElement
{
	[Export] private Button Button;
	public Craft craft;
	public int listIndex = -1;

	protected override Task _Setup()
	{
		if (Button == null)
		{
			GD.PrintErr("CraftButtonUI.Setup(): Button is null");
			return Task.CompletedTask;
		}

		Button.Text = listIndex.ToString();
		if (craft != null)
			Button.TooltipText = $"{craft.ItemName} ({craft.Status})";

		if (!Button.IsConnected(Button.SignalName.Pressed, Callable.From(ButtonOnPressed)))
			Button.Pressed += ButtonOnPressed;

		return Task.CompletedTask;
	}

	public override void _ExitTree()
	{
		if (Button != null &&
		    Button.IsConnected(Button.SignalName.Pressed, Callable.From(ButtonOnPressed)))
		{
			Button.Pressed -= ButtonOnPressed;
		}

		base._ExitTree();
	}

	private void ButtonOnPressed()
	{
		if (craft == null)
		{
			GD.PrintErr("CraftButtonUI.ButtonOnPressed(): craft is null");
			return;
		}

		OrbitalCamera camera = OrbitalCamera.Instance;
		if (camera == null)
		{
			GD.PrintErr("CraftButtonUI.ButtonOnPressed(): OrbitalCamera is null");
			return;
		}

		_ = camera.FocusOnCell(craft.CurrentCellIndex);
	}
}
