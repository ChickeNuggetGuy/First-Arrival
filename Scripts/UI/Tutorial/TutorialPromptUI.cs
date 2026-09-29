using Godot;

public partial class TutorialPromptUI : CanvasLayer
{
	private Label _title;
	private Label _description;
	private ProgressBar _progress;

	public override void _Ready()
	{
		Layer = 5;
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		AddChild(root);
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		MissionUITheme.Apply(root);

		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(panel);
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
		panel.OffsetLeft = -230;
		panel.OffsetRight = 230;
		panel.OffsetTop = 24;
		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 16);
		panel.AddChild(margin);
		var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		content.AddThemeConstantOverride("separation", 8);
		margin.AddChild(content);
		_title = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		MissionUITheme.StyleTitle(_title, 22);
		content.AddChild(_title);
		_description = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_description);
		_progress = new ProgressBar
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0, 22),
			MinValue = 0,
			MaxValue = 100,
			Step = 0.1,
			ShowPercentage = true
		};
		MissionUITheme.StyleProgressBar(_progress, MissionUITheme.AccentColor);
		content.AddChild(_progress);
	}

	public void SetProgress(float progress) => _progress.Value = progress * 100.0f;

	public void SetPrompt(string title, string description)
	{
		_title.Text = title;
		_description.Text = description;
	}
}
