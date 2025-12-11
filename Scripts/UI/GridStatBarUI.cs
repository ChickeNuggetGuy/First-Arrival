using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridStatBarUI : UIElement
{
	[Export] ProgressBar statBar;
	[Export] private Enums.Stat stat;
	[Export] private Enums.UnitTeam team;

	private GridObjectStat _stat;

	protected override async Task _Setup()
	{
		if (statBar == null)
		{
			GD.PushError("statBar is not assigned!");
			return;
		}

		var sb = new StyleBoxFlat();

		switch (stat)
		{
			case Enums.Stat.None:
				sb.BgColor = Colors.Black;
				break;
			case Enums.Stat.Health:
				sb.BgColor = Colors.Tomato;
				break;
			case Enums.Stat.Stamina:
				sb.BgColor = Colors.Gold;
				break;
			case Enums.Stat.Bravery:
				sb.BgColor = Colors.Indigo;
				break;
			case Enums.Stat.TimeUnits:
				sb.BgColor = Colors.ForestGreen;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		statBar.AddThemeStyleboxOverride("fill", sb);
	}

	public void SetupStatBar(GridObject gridObject)
	{
		if(gridObject == null) return;
		if (_stat != null)
		{
			_stat.CurrentValueChanged -= StatOnCurrentValueChanged;
			_stat = null;
		}
		if(!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return;
		GridObjectStat gridObjectStat = statHolder.Stats.FirstOrDefault(s => s.Stat == stat);
		if (gridObjectStat == null) return;

		_stat = gridObjectStat;
		statBar.MinValue = _stat.MinMaxValue.min;
		statBar.MaxValue = _stat.MinMaxValue.max;
		statBar.Value = _stat.CurrentValue;

		_stat.CurrentValueChanged += StatOnCurrentValueChanged;
	}

	private void StatOnCurrentValueChanged(int value, GridObject gridObject)
	{
		statBar.Value = value;
	}
}