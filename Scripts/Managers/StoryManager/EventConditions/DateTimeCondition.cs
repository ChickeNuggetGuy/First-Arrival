using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

/// <summary>
/// Becomes true when the strategic clock reaches the configured date, time, or
/// combined date-time. Disabled portions act as wildcards.
/// </summary>
[GlobalClass]
public partial class DateTimeCondition : EventCondition
{
	[ExportGroup("Check Options")]
	[Export] public bool CheckDate { get; private set; } = true;
	[Export] public bool CheckTime { get; private set; }

	[ExportGroup("Target Date")]
	[Export(PropertyHint.Range, "1,9999,1,or_greater")]
	public int Year { get; private set; } = 2001;
	[Export] public Enums.Month Month { get; private set; } = Enums.Month.September;
	[Export(PropertyHint.Range, "1,31,1")]
	public int DayOfMonth { get; private set; } = 5;

	[ExportGroup("Target Time")]
	[Export(PropertyHint.Range, "0,23,1")]
	public int Hour { get; private set; }
	[Export(PropertyHint.Range, "0,59,1")]
	public int Minute { get; private set; }
	[Export(PropertyHint.Range, "0,59,1")]
	public int Second { get; private set; }

	private bool _reportedInvalidConfiguration;

	/// <summary>
	/// Uses at-or-after comparisons so a condition cannot be skipped when the game
	/// clock advances by more than one second in a frame.
	/// </summary>
	public override bool Check()
	{
		GlobeTimeManager clock = GlobeTimeManager.Instance;
		if (clock == null) return false;

		if (!HasValidConfiguration())
		{
			if (!_reportedInvalidConfiguration)
			{
				GD.PushError("DateTimeCondition has an invalid target date or time.");
				_reportedInvalidConfiguration = true;
			}
			return false;
		}
		_reportedInvalidConfiguration = false;

		// Both disabled means any date and any time.
		if (!CheckDate && !CheckTime) return true;

		int dateComparison = 0;
		if (CheckDate)
		{
			dateComparison = CompareDate(clock);
			if (!CheckTime) return dateComparison >= 0;
		}

		int timeComparison = CompareTime(clock);
		if (!CheckDate) return timeComparison >= 0;

		// For a combined check, a later date always passes. On the target date,
		// the configured time must also have been reached.
		return dateComparison > 0 ||
		       (dateComparison == 0 && timeComparison >= 0);
	}

	private int CompareDate(GlobeTimeManager clock)
	{
		int comparison = clock.CurrentYear.CompareTo(Year);
		if (comparison != 0) return comparison;

		comparison = ((int)clock.CurrentMonth).CompareTo((int)Month);
		return comparison != 0
			? comparison
			: clock.CurrentDayOfMonth.CompareTo(DayOfMonth);
	}

	private int CompareTime(GlobeTimeManager clock)
	{
		int currentSeconds =
			(clock.CurrentHour * 3600) +
			(clock.CurrentMinute * 60) +
			clock.CurrentSeconds;
		int targetSeconds = (Hour * 3600) + (Minute * 60) + Second;
		return currentSeconds.CompareTo(targetSeconds);
	}

	private bool HasValidConfiguration()
	{
		if (CheckDate)
		{
			int monthNumber = (int)Month;
			if (Year < 1 || monthNumber is < 1 or > 12) return false;
			if (DayOfMonth < 1 || DayOfMonth > GetDaysInMonth(Month)) return false;
		}

		return !CheckTime ||
		       (Hour is >= 0 and <= 23 &&
		        Minute is >= 0 and <= 59 &&
		        Second is >= 0 and <= 59);
	}

	private static int GetDaysInMonth(Enums.Month month)
	{
		return month switch
		{
			Enums.Month.February => 28,
			Enums.Month.April or
			Enums.Month.June or
			Enums.Month.September or
			Enums.Month.November => 30,
			_ => 31
		};
	}
}
