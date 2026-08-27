using System.Threading.Tasks;
using Godot;

public partial class TextPopupElement : PopupElement
{
	public Label Label { get; private set; }
	public string Text { get; private set; } = string.Empty;

	public void SetText(string text)
	{
		Text = text ?? string.Empty;
		if (Label != null) Label.Text = Text;
	}

	protected override async Task _Setup()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		MouseFilter = MouseFilterEnum.Ignore;

		if (Label == null)
		{
			Label = new Label
			{
				Name = "Text",
				Text = Text,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Ignore
			};
			AddChild(Label);
			Label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			Label.MinimumSizeChanged += UpdateMinimumHeight;
		}
		else
		{
			Label.Text = Text;
		}

		// Allow the VBoxContainer to assign a width before asking the wrapping
		// label how much vertical space it needs.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		UpdateMinimumHeight();
	}

	private void UpdateMinimumHeight()
	{
		if (Label == null) return;
		CustomMinimumSize = new Vector2(
			0,
			Mathf.Max(Label.GetCombinedMinimumSize().Y, 24f));
	}

	public override void _ExitTree()
	{
		if (Label != null)
			Label.MinimumSizeChanged -= UpdateMinimumHeight;
		base._ExitTree();
	}
}
