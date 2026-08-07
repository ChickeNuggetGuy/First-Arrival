using Godot;

public sealed class StatProgressBarOverlay
{
	private readonly ProgressBar _progressBar;
	private readonly ColorRect _restrictedRange;
	private readonly ColorRect _fatalWoundRange;
	private readonly ColorRect _fatalWoundMarker;
	private readonly ColorRect _unaffordablePreview;

	private float _effectiveMaxRatio = 1f;
	private float _fatalWoundRatio;

	public StatProgressBarOverlay(ProgressBar progressBar)
	{
		_progressBar = progressBar;
		_progressBar.ClipContents = true;

		_restrictedRange = CreateOverlay(
			"RestrictedRange",
			new Color(0.12f, 0.12f, 0.12f, 0.72f)
		);
		_fatalWoundRange = CreateOverlay(
			"FatalWoundRange",
			new Color(0.45f, 0.02f, 0.02f, 0.72f)
		);
		_fatalWoundMarker = CreateOverlay(
			"FatalWoundMarker",
			new Color(1f, 0.75f, 0.1f, 1f)
		);
		_unaffordablePreview = CreateOverlay(
			"UnaffordableActionPreview",
			new Color(0.85f, 0.02f, 0.02f, 0.78f)
		);

		_progressBar.Resized += LayoutOverlays;
	}

	public void Update(
		GridObjectStatHolder statHolder,
		GridObjectStat stat,
		int previewCost = 0
	)
	{
		float originalMin = stat.MinMaxValue.min;
		float originalMax = stat.MinMaxValue.max;
		float range = Mathf.Max(1f, originalMax - originalMin);
		float effectiveMax = statHolder.GetEffectiveMaxValue(stat.Stat);

		_progressBar.MinValue = originalMin;
		_progressBar.MaxValue = originalMax;
		float currentValue = statHolder.GetEffectiveCurrentValue(stat.Stat);
		float remainingValue = currentValue - Mathf.Max(0, previewCost);
		_progressBar.Value = Mathf.Max(originalMin, remainingValue);
		_unaffordablePreview.Visible = previewCost > currentValue;

		_effectiveMaxRatio = Mathf.Clamp(
			(effectiveMax - originalMin) / range,
			0f,
			1f
		);
		_restrictedRange.Visible = effectiveMax < originalMax;

		int fatalWounds = stat.Stat == FirstArrival.Scripts.Utility.Enums.Stat.Health
			? stat.GetTotalFatalWounds()
			: 0;
		_fatalWoundRatio = Mathf.Clamp(
			(fatalWounds - originalMin) / range,
			0f,
			1f
		);
		bool showFatalWounds = fatalWounds > originalMin;
		_fatalWoundRange.Visible = showFatalWounds;
		_fatalWoundMarker.Visible = showFatalWounds;

		_progressBar.TooltipText = BuildTooltip(
			stat,
			currentValue,
			effectiveMax,
			originalMax,
			fatalWounds,
			previewCost
		);
		LayoutOverlays();
	}

	public void Dispose()
	{
		_progressBar.Resized -= LayoutOverlays;
	}

	private ColorRect CreateOverlay(string name, Color color)
	{
		var overlay = new ColorRect
		{
			Name = name,
			Color = color,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_progressBar.AddChild(overlay);
		return overlay;
	}

	private void LayoutOverlays()
	{
		float width = _progressBar.Size.X;
		float height = _progressBar.Size.Y;
		float effectiveMaxX = width * _effectiveMaxRatio;
		float fatalWoundX = width * _fatalWoundRatio;

		_restrictedRange.Position = new Vector2(effectiveMaxX, 0);
		_restrictedRange.Size = new Vector2(
			Mathf.Max(0, width - effectiveMaxX),
			height
		);
		_fatalWoundRange.Position = Vector2.Zero;
		_fatalWoundRange.Size = new Vector2(fatalWoundX, height);
		_fatalWoundMarker.Position = new Vector2(
			Mathf.Clamp(fatalWoundX - 1f, 0, Mathf.Max(0, width - 2f)),
			0
		);
		_fatalWoundMarker.Size = new Vector2(Mathf.Min(2f, width), height);
		_unaffordablePreview.Position = Vector2.Zero;
		_unaffordablePreview.Size = new Vector2(width, height);
	}

	private static string BuildTooltip(
		GridObjectStat stat,
		float currentValue,
		float effectiveMax,
		float originalMax,
		int fatalWounds,
		int previewCost
	)
	{
		string text =
			$"{stat.Stat}: {currentValue:0.#} / {effectiveMax:0.#}";
		if (effectiveMax < originalMax)
		{
			text += $" (original max {originalMax:0.#})";
		}
		if (previewCost > 0)
		{
			text += $"\nAction cost: {previewCost}"
			        + $"\nRemaining: {currentValue - previewCost:0.#}";
		}
		if (fatalWounds > 0)
		{
			text += $"\nFatal-wound threshold: {fatalWounds}";
		}
		return text;
	}
}
