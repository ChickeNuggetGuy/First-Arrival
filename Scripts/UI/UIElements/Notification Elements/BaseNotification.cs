using System;

/// <summary>
/// Opens the player base at the target globe cell when clicked.
/// Navigation is delegated to GlobeUI so notification and regular base buttons
/// share the same transition guard and state-stashing flow.
/// </summary>
public partial class BaseNotification : NotificationElement
{
	private readonly int targetBaseCellIndex;
	private readonly Action<int> openBase;

	public BaseNotification(
		string text,
		float duration,
		int targetBaseCellIndex,
		Action<int> openBase) : base(text, duration)
	{
		this.targetBaseCellIndex = targetBaseCellIndex;
		this.openBase = openBase;
	}

	protected override void OnClicked()
	{
		openBase?.Invoke(targetBaseCellIndex);
	}
}
