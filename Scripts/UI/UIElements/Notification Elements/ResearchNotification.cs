using System;

/// <summary>Opens the current globe research screen when clicked.</summary>
public partial class ResearchNotification : NotificationElement
{
	private readonly Action openResearch;

	public ResearchNotification(
		string text,
		float duration,
		Action openResearch) : base(text, duration)
	{
		this.openResearch = openResearch;
	}

	protected override void OnClicked()
	{
		QueueFree();
		openResearch?.Invoke();
	}
}
