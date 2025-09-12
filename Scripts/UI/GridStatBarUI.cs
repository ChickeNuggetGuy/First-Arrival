using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridStatBarUI : UIElement
{
	[Export] ProgressBar statBar;
	[Export] private Enums.Stat stat;
	private GridObject _currentGridObject;
	[Export] private Enums.UnitTeam team;
	private GridObjectStat _stat;
	[Export] Color fillColor;
	protected override async Task _Setup()
	{
		GridObjectTeamHolder teamHolder =GridObjectManager.Instance.GetGridObjectTeamHolder(team);

		var sb = new StyleBoxFlat();
		
		switch (stat)
		{
			case Enums.Stat.None:
				fillColor = Colors.Black;
				break;
			case Enums.Stat.Health:
				fillColor = Colors.Tomato;
				break;
			case Enums.Stat.Stamina:
				fillColor = Colors.Gold;
				break;
			case Enums.Stat.Bravery:
				fillColor = Colors.Indigo;
				break;
			case Enums.Stat.TimeUnits:
				fillColor = Colors.ForestGreen;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		StyleBoxFlat styleBox = new StyleBoxFlat();
		statBar.AddThemeStyleboxOverride("fill", sb);
		sb.BgColor = fillColor;
		;
	}
	
	public void SetupStatBar(GridObject gridObject)
	{
		if (_stat != null)
		{
			//Clear any previous stat references and event listeners
			_stat.CurrentValueChanged -= StatOnCurrentValueChanged;
			_stat = null;
		}
		
		

		GridObjectStat gridObjectStat = gridObject.Stats.FirstOrDefault(stat => stat.Stat == this.stat);
		if (gridObjectStat == null) return;
		
		_stat = gridObjectStat;
		statBar.MinValue = _stat.MinMaxValue.min;
		statBar.MaxValue = _stat.MinMaxValue.max;
		statBar.Value = _stat.CurrentValue;
		
		_stat.CurrentValueChanged += StatOnCurrentValueChanged;
		
	}

	private void StatOnCurrentValueChanged(int value)
	{
		statBar.Value = value;
	}
}
