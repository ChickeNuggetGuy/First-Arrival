using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;


//TODO: Current Value Fill not actually working. The entire bar is filled regardless
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
		}
	}


	public void UpdateStat(GridObjectStatHolder targetGridObjectStatHolder)
	{
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
		
		
		GD.Print($"Updating Stat {targetStat}");
		statProgressBar.MinValue = stat.MinMaxValue.min;
		statProgressBar.MaxValue = stat.MinMaxValue.max;
		statProgressBar.SetValue(stat.CurrentValue);
	}
	
	public void SetProgressColor(Color newColor, Control targetControl)
	{
		var styleBoxFlat = new StyleBoxFlat();

		styleBoxFlat.BgColor = newColor;

		targetControl.AddThemeStyleboxOverride("fill", styleBoxFlat);
	}

}
