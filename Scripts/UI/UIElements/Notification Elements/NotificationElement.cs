using Godot;
using System;
using System.Threading.Tasks;

public abstract partial class NotificationElement : UIElement
{
	private Label _label;
	private Button _button;
	private Timer _timer;
	[Export] private float duration = 3.5f;
	private string targetText;


	public NotificationElement(string text, float duration)
	{
		CustomMinimumSize = new Vector2(0, 25);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		
		targetText = text;
		if (_label == null)
		{
			_label = new Label();
			AddChild(_label);
			_label.Text = targetText;
			_label.MouseFilter = MouseFilterEnum.Ignore;
			_label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}

		if (_button == null)
		{
			_button = new Button();
			AddChild(_button);
			_button.Pressed += OnClickedCall;
			_button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			MoveChild(_button, 0);
		}

		if (_timer == null)
		{
			_timer = new Timer();
			_timer.WaitTime = duration;
			_timer.Timeout += TimeOut;
			_timer.Autostart = true;
			AddChild(_timer);
		}
	}
	
	protected override Task _Setup()
	{
		return Task.CompletedTask;
	}

	public void OnClickedCall()
	{
		OnClicked();
	}


	protected abstract void OnClicked();


	protected void TimeOut()
	{
		QueueFree();
	}

	public override void _ExitTree()
	{
		if (NotificationsUI.Instance != null &&
			GodotObject.IsInstanceValid(NotificationsUI.Instance))
		{
			NotificationsUI.Instance.RemoveNotification(this);
		}
		if (_button != null)
			_button.Pressed -= OnClickedCall;
		base._ExitTree();

	}
}
