using Godot;

public enum TutorialControlAction
{
	KeyboardPan,
	MousePan,
	OpenCellContextMenu,
	FocusCell
}

public enum TutorialActionMeasurement
{
	Amount,
	ActiveSeconds,
	SecondsAfterAction
}

[GlobalClass]
public partial class PlayerActionCondition : EventCondition
{
	[Export] public TutorialControlAction Action { get; private set; }
	[Export] public TutorialActionMeasurement Measurement { get; private set; }
	[Export(PropertyHint.Range, "0.01,100,0.01,or_greater")]
	public float RequiredAmount { get; private set; } = 1.0f;

	public float GetProgress()
	{
		TutorialManager manager = TutorialManager.Instance;
		if (manager == null) return 0.0f;
		float amount = Measurement switch
		{
			TutorialActionMeasurement.ActiveSeconds => manager.GetActionDuration(Action),
			TutorialActionMeasurement.SecondsAfterAction => manager.GetTimeSinceAction(Action),
			_ => manager.GetActionAmount(Action)
		};
		return Mathf.Clamp(amount / Mathf.Max(RequiredAmount, 0.01f), 0.0f, 1.0f);
	}

	public override bool Check() => GetProgress() >= 1.0f;
}
