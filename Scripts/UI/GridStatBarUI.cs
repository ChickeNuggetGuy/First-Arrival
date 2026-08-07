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
	private GridObjectStat _healthStat;
	private GridObjectStatHolder _statHolder;
	private StatProgressBarOverlay _overlay;
	private int _previewCost;

	protected override async Task _Setup()
	{
		if (statBar == null)
		{
			GD.PushError("statBar is not assigned!");
			return;
		}

		var sb = new StyleBoxFlat
		{
			BgColor = Enums.statColors.TryGetValue(stat, out Color color)
				? color
				: Colors.Black
		};

		statBar.AddThemeStyleboxOverride("fill", sb);
		_overlay ??= new StatProgressBarOverlay(statBar);
	}

	public void SetupStatBar(GridObject gridObject)
	{
		UnbindStat();
		if(gridObject == null) return;
		if(!gridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return;
		GridObjectStat gridObjectStat = statHolder.Stats.FirstOrDefault(s => s.Stat == stat);
		if (gridObjectStat == null) return;

		_statHolder = statHolder;
		_stat = gridObjectStat;
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
		if (_statHolder == null || _stat == null) return;
		_overlay ??= new StatProgressBarOverlay(statBar);
		_overlay.Update(_statHolder, _stat, _previewCost);
	}

	public void SetPreviewCosts(
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	)
	{
		_previewCost = costs != null && costs.TryGetValue(stat, out int cost)
			? Mathf.Max(0, cost)
			: 0;
		Refresh();
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
}
