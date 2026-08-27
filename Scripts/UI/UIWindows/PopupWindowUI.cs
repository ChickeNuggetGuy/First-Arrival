using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public sealed class PopupResult
{
	public string ActionId { get; }
	public bool WasCancelled { get; }

	public PopupResult(string actionId, bool wasCancelled = false)
	{
		ActionId = actionId ?? string.Empty;
		WasCancelled = wasCancelled;
	}
}

/// <summary>
/// A single reusable popup renderer. Callers provide content; the window creates
/// the required controls and completes the returned task after player input.
/// </summary>
public partial class PopupWindowUI : UIWindow
{
	private sealed class PendingPopup
	{
		public string Title { get; init; } = string.Empty;
		public string[] TextBlocks { get; init; } = Array.Empty<string>();
		public string ContinueButtonText { get; init; } = "Continue";
		public TaskCompletionSource<PopupResult> Completion { get; } = new();
	}

	private readonly Queue<PendingPopup> _popupQueue = new();
	private PendingPopup _currentPopup;
	private VBoxContainer popupContainer;
	private Label _titleLabel;
	private Button _continueButton;
	private bool _setupComplete;
	private bool _changingPopup;

	/// <summary>
	/// Queues a popup containing one dynamically generated text element.
	/// </summary>
	public Task<PopupResult> ShowTextPopupAsync(
		string title,
		string text,
		string continueButtonText = "Continue")
	{
		return ShowTextPopupAsync(
			title,
			new[] { text ?? string.Empty },
			continueButtonText);
	}

	/// <summary>
	/// Queues a popup with one generated TextPopupElement per supplied block.
	/// This is the extension point for future decision and image elements.
	/// </summary>
	public Task<PopupResult> ShowTextPopupAsync(
		string title,
		IEnumerable<string> textBlocks,
		string continueButtonText = "Continue")
	{
		var blocks = new List<string>();
		if (textBlocks != null)
		{
			foreach (string block in textBlocks)
				blocks.Add(block ?? string.Empty);
		}
		if (blocks.Count == 0) blocks.Add(string.Empty);

		var pending = new PendingPopup
		{
			Title = title ?? string.Empty,
			TextBlocks = blocks.ToArray(),
			ContinueButtonText = string.IsNullOrWhiteSpace(continueButtonText)
				? "Continue"
				: continueButtonText.Trim()
		};

		_popupQueue.Enqueue(pending);
		_ = TryShowNextPopupAsync();
		return pending.Completion.Task;
	}

	protected override Task _Setup()
	{
		BuildWindowIfNeeded();
		MissionUITheme.Apply(this);
		MissionUITheme.StyleTitle(_titleLabel, 26);
		MissionUITheme.StyleButton(_continueButton);
		ProcessMode = ProcessModeEnum.Always;
		_setupComplete = true;
		_ = TryShowNextPopupAsync();
		return Task.CompletedTask;
	}

	protected override async Task DrawUI()
	{
		if (_currentPopup == null || popupContainer == null) return;

		_titleLabel.Text = _currentPopup.Title;
		_continueButton.Text = _currentPopup.ContinueButtonText;
		_continueButton.Disabled = false;
		ClearPopupElements();

		foreach (string text in _currentPopup.TextBlocks)
		{
			var element = new TextPopupElement { Name = "TextPopupElement" };
			element.SetText(text);
			popupContainer.AddChild(element);
			await element.SetupCall();
		}
	}

	private void BuildWindowIfNeeded()
	{
		if (popupContainer != null && _titleLabel != null && _continueButton != null)
			return;

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		var backdrop = new ColorRect
		{
			Name = "Backdrop",
			Color = MissionUITheme.BackdropColor,
			MouseFilter = MouseFilterEnum.Stop
		};
		AddChild(backdrop);
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visual = backdrop;

		var center = new CenterContainer { Name = "CenterContainer" };
		backdrop.AddChild(center);
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var panel = new PanelContainer
		{
			Name = "PopupPanel",
			CustomMinimumSize = new Vector2(680, 360)
		};
		center.AddChild(panel);

		var margin = new MarginContainer { Name = "MarginContainer" };
		margin.AddThemeConstantOverride("margin_left", 30);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_right", 30);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "PopupLayout",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		layout.AddThemeConstantOverride("separation", 14);
		margin.AddChild(layout);

		_titleLabel = new Label
		{
			Name = "Title",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		layout.AddChild(_titleLabel);
		layout.AddChild(new HSeparator());

		var scrollContainer = new ScrollContainer
		{
			Name = "ContentScroll",
			CustomMinimumSize = new Vector2(0, 190),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		layout.AddChild(scrollContainer);

		popupContainer = new VBoxContainer
		{
			Name = "PopupElements",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		popupContainer.AddThemeConstantOverride("separation", 12);
		scrollContainer.AddChild(popupContainer);

		layout.AddChild(new HSeparator());
		_continueButton = new Button
		{
			Name = "ContinueButton",
			Text = "Continue",
			CustomMinimumSize = new Vector2(0, 48),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_continueButton.Pressed += ContinueButtonOnPressed;
		layout.AddChild(_continueButton);
	}

	private async Task TryShowNextPopupAsync()
	{
		if (!_setupComplete || _changingPopup || _currentPopup != null ||
		    _popupQueue.Count == 0 || !IsInsideTree())
			return;

		_changingPopup = true;
		_currentPopup = _popupQueue.Dequeue();
		try
		{
			await ShowCall();
		}
		catch (Exception exception)
		{
			GD.PushError($"Popup window could not be shown: {exception.Message}");
			PendingPopup failedPopup = _currentPopup;
			_currentPopup = null;
			failedPopup.Completion.TrySetResult(new PopupResult(string.Empty, true));
		}
		finally
		{
			_changingPopup = false;
		}

		if (_currentPopup == null) _ = TryShowNextPopupAsync();
	}

	private async void ContinueButtonOnPressed()
	{
		if (_currentPopup == null || _changingPopup) return;

		_changingPopup = true;
		_continueButton.Disabled = true;
		PendingPopup completedPopup = _currentPopup;
		try
		{
			await HideCall();
		}
		finally
		{
			_currentPopup = null;
			_changingPopup = false;
			completedPopup.Completion.TrySetResult(new PopupResult("continue"));
			_ = TryShowNextPopupAsync();
		}
	}

	private void ClearPopupElements()
	{
		if (popupContainer == null) return;
		foreach (Node child in popupContainer.GetChildren())
		{
			popupContainer.RemoveChild(child);
			child.QueueFree();
		}
	}

	public override void _ExitTree()
	{
		if (_continueButton != null)
			_continueButton.Pressed -= ContinueButtonOnPressed;

		var cancelledResult = new PopupResult(string.Empty, true);
		_currentPopup?.Completion.TrySetResult(cancelledResult);
		_currentPopup = null;
		while (_popupQueue.Count > 0)
			_popupQueue.Dequeue().Completion.TrySetResult(cancelledResult);

		base._ExitTree();
	}
}
