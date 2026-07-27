using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass, Tool]
public partial class TextStatBar : UIElement
{
	private Enums.Stat _targetStat;
	[Export]
	public Enums.Stat targetStat
	{
		get
		{
			return _targetStat;
		}
		set
		{
			_targetStat = value;
			if (_statNameLabel != null)
			{
				_statNameLabel.Text = _targetStat.ToString();
			}
		}
	}
	
	[ExportGroup("Label Settings "), Export] private Label _statNameLabel;
	[Export(PropertyHint.Range, "25,250,")] private float _labelWidth;
	[Export] private HorizontalAlignment _horizontalAlignment;
	[Export] private VerticalAlignment _verticalAlignment;
	
	[ExportGroup("Progress Bar Settings"),Export] private ProgressBar statProgressBar;
	private GridObjectStatHolder _statHolder;
	private GridObjectStat _stat;
	private GridObjectStat _healthStat;
	private StatProgressBarOverlay _overlay;

	protected override async Task _Setup()
	{
		if(_statNameLabel != null)
		{
			_statNameLabel.SetSize(new Vector2(_labelWidth, _statNameLabel.Size.Y));
			_statNameLabel.Text = targetStat.ToString();
			_statNameLabel.HorizontalAlignment = _horizontalAlignment;
			_statNameLabel.VerticalAlignment = _verticalAlignment;
		}

		if (statProgressBar != null)
		{
			SetProgressColor(Enums.statColors[targetStat], statProgressBar);
			_overlay ??= new StatProgressBarOverlay(statProgressBar);
		}
	}


	public void UpdateStat(GridObjectStatHolder targetGridObjectStatHolder)
	{
		UnbindStat();
		if (targetGridObjectStatHolder == null)
		{
			GD.PrintErr("StatBar UpdateStat: targetGridObjectStatHolder is null");
			return;
		}
		if (!targetGridObjectStatHolder.TryGetStat(targetStat, out var stat))
		{
			GD.PrintErr($"StatBar UpdateStat: stat: { targetStat} is null");
			return;
		}
		if(statProgressBar == null)
		{
			GD.PrintErr("StatBar UpdateStat: statProgressBar is null");
			return;
		}
		
		_statHolder = targetGridObjectStatHolder;
		_stat = stat;
		_statHolder.TryGetStat(Enums.Stat.Health, out _healthStat);
		_stat.CurrentValueChanged += StatOnCurrentValueChanged;
		if (_healthStat != null)
		{
			_healthStat.FatalWoundsChanged += HealthOnFatalWoundsChanged;
		}
		Refresh();
	}

	private void StatOnCurrentValueChanged(int value, GridObject gridObject)
	{
		Refresh();
	}

	private void HealthOnFatalWoundsChanged(
		int totalWounds,
		int bodyPart,
		int woundsOnBodyPart,
		GridObject gridObject
	)
	{
		Refresh();
	}

	private void Refresh()
	{
		if (_statHolder == null || _stat == null || statProgressBar == null) return;
		_overlay ??= new StatProgressBarOverlay(statProgressBar);
		_overlay.Update(_statHolder, _stat);
	}

	private void UnbindStat()
	{
		if (_stat != null)
		{
			_stat.CurrentValueChanged -= StatOnCurrentValueChanged;
		}
		if (_healthStat != null)
		{
			_healthStat.FatalWoundsChanged -= HealthOnFatalWoundsChanged;
		}
		_stat = null;
		_healthStat = null;
		_statHolder = null;
	}

	public override void _ExitTree()
	{
		UnbindStat();
		_overlay?.Dispose();
		base._ExitTree();
	}
	
	public void SetProgressColor(Color newColor, Control targetControl)
	{
		var styleBoxFlat = new StyleBoxFlat();

		styleBoxFlat.BgColor = newColor;

		targetControl.AddThemeStyleboxOverride("fill", styleBoxFlat);
	}

}
