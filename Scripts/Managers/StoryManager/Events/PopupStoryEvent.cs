using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;

/// <summary>
/// Displays authored story text through the shared dynamic popup window and waits
/// for the player to dismiss it before completing the story event.
/// </summary>
[GlobalClass]
public partial class PopupStoryEvent : StoryEvent
{
	[Export] public string PopupTitle { get; private set; } = string.Empty;
	[Export(PropertyHint.MultilineText)]
	public string PopupText { get; private set; } = string.Empty;
	[Export] public string ContinueButtonText { get; private set; } = "Continue";

	protected override async Task<StoryEventExecutionResult> Execute()
	{
		PopupWindowUI popupWindow = UIManager.Instance?.GetWindow<PopupWindowUI>();
		if (popupWindow == null)
			return StoryEventExecutionResult.Blocked;

		string title = string.IsNullOrWhiteSpace(PopupTitle)
			? EventName
			: PopupTitle;
		string text = string.IsNullOrWhiteSpace(PopupText)
			? EventDescription
			: PopupText;

		PopupResult result = await popupWindow.ShowTextPopupAsync(
			title,
			text,
			ContinueButtonText);
		return result.WasCancelled
			? StoryEventExecutionResult.Cancelled
			: StoryEventExecutionResult.Completed;
	}
}
