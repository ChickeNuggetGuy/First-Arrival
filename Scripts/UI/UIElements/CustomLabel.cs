using Godot;
using System.Threading.Tasks;

[GlobalClass]
public partial class CustomLabel : UIElement
{
	private Label label;
	public Label Label => EnsureLabel();
	[Export] protected string prependText = string.Empty;
	[Export] protected string appendText = string.Empty;

	private string text;
	public string Text
	{
		get => text;
		set
		{
			text = value ?? string.Empty;
			EnsureLabel().Text = prependText + text + appendText;
		}
	}

	protected override Task _Setup()
	{
		if (CustomMinimumSize.X  == 0 && CustomMinimumSize.Y == 0)
		{
			 CustomMinimumSize = new Vector2(0, 25);
		}
		EnsureLabel();
		return Task.CompletedTask;
	}

	private Label EnsureLabel()
	{
		if (label != null && GodotObject.IsInstanceValid(label))
		{
			return label;
		}

		label = GetNodeOrNull<Label>("Label");
		if (label != null) return label;

		label = new Label { Name = "Label" };
		AddChild(label);
		return label;
	}
}
