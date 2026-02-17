using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class GlobeTimeManager : Manager<GlobeTimeManager>
{
	[Export] public int timeSpeed = 1;
	[Export] private Label currentTimeUI;

	[ExportGroup("Sun / Day-Night")]
	[Export] private DirectionalLight3D sunLight;
	[Export(PropertyHint.Range, "0,45,0.01")]
	private float axialTiltDegrees = 23.44f;
	[Export(PropertyHint.Range, "-180,180,0.1")]
	private float sunTimeOffsetDegrees = 0f;
	[Export(PropertyHint.Range, "0,20,0.01")]
	private float sunFollow = 6f;

	public int CurrentYear { get; private set; } = 2001;
	public Enums.Month CurrentMonth { get; private set; } = Enums.Month.September;
	public int CurrentDayOfMonth { get; private set; } = 5;
	public int CurrentDayOfYear { get; private set; }
	public Enums.Day CurrentDay { get; private set; } = Enums.Day.Wednesday;

	public int CurrentHour { get; private set; } = 12;
	public int CurrentMinute { get; private set; } = 26;
	public int CurrentSeconds { get; private set; } = 1;

	private const int SecondsPerDay = 24 * 60 * 60;

	private readonly Dictionary<Enums.Month, int> daysInMonth = new()
	{
		{ Enums.Month.January, 31 },
		{ Enums.Month.February, 28 },
		{ Enums.Month.March, 31 },
		{ Enums.Month.April, 30 },
		{ Enums.Month.May, 31 },
		{ Enums.Month.June, 30 },
		{ Enums.Month.July, 31 },
		{ Enums.Month.August, 31 },
		{ Enums.Month.September, 30 },
		{ Enums.Month.October, 31 },
		{ Enums.Month.November, 30 },
		{ Enums.Month.December, 31 }
	};

	private static readonly Enums.Month[] MonthOrder =
	{
		Enums.Month.January,
		Enums.Month.February,
		Enums.Month.March,
		Enums.Month.April,
		Enums.Month.May,
		Enums.Month.June,
		Enums.Month.July,
		Enums.Month.August,
		Enums.Month.September,
		Enums.Month.October,
		Enums.Month.November,
		Enums.Month.December
	};

	private Timer timer;
	private int secondsOfDay;

	#region Signals
	[Signal]
	public delegate void DateChangedEventHandler(
		int year,
		Enums.Month month,
		int dayOfMonth,
		Enums.Day day
	);

	[Signal]
	public delegate void TimeChangedEventHandler(
		int hour,
		int minute,
		int second
	);

	[Signal]
	public delegate void DayChangedEventHandler(
		int dayOfYear,
		int dayOfMonth,
		Enums.Day day
	);

	[Signal]
	public delegate void MonthChangedEventHandler(Enums.Month month);

	[Signal]
	public delegate void YearChangedEventHandler(int year);
	#endregion

	public override string GetManagerName() => "GlobeTimeManager";

	protected override Task _Setup(bool loadingData)
	{
		RecomputeDerivedDateFields();
		secondsOfDay = (CurrentHour * 3600) + (CurrentMinute * 60) + CurrentSeconds;

		timer = new Timer
		{
			WaitTime = .05f,
			OneShot = false,
			Autostart = false,
			Paused = true
		};

		timer.Timeout += OnTimerTimeout;
		AddChild(timer);

		UpdateUI();
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		timer.Paused = false;
		timer.Start();
		return Task.CompletedTask;
	}

	public override void Deinitialize()
	{
		if (timer != null)
		{
			timer.Timeout -= OnTimerTimeout;
			timer.Stop();
		}
	}

	public override Dictionary<string, Variant> Save()
	{
		return new();
	}

	public override void _Process(double delta)
	{
		UpdateSunLight(delta);
	}

	private void OnTimerTimeout()
	{
		int add = Math.Max(0, timeSpeed);
		AdvanceTimeBySeconds(add);

		UpdateUI();
		EmitSignal(
			SignalName.TimeChanged,
			CurrentHour,
			CurrentMinute,
			CurrentSeconds
		);
	}

	private void AdvanceTimeBySeconds(int secondsToAdd)
	{
		if (secondsToAdd <= 0) return;

		long total = (long)secondsOfDay + secondsToAdd;
		int daysToAdvance = (int)(total / SecondsPerDay);
		secondsOfDay = (int)(total % SecondsPerDay);

		if (daysToAdvance > 0)
		{
			AdvanceDateByDays(daysToAdvance);
		}

		CurrentHour = secondsOfDay / 3600;
		CurrentMinute = (secondsOfDay % 3600) / 60;
		CurrentSeconds = secondsOfDay % 60;
	}

	private void AdvanceDateByDays(int days)
	{
		int dowShift = days % 7;
		CurrentDay = (Enums.Day)((((int)CurrentDay - 1 + dowShift) % 7) + 1);

		bool monthChanged = false;
		bool yearChanged = false;

		while (days > 0)
		{
			int dim = daysInMonth[CurrentMonth];
			int remainingInMonth = dim - CurrentDayOfMonth;

			if (days <= remainingInMonth)
			{
				CurrentDayOfMonth += days;
				days = 0;
			}
			else
			{
				days -= (remainingInMonth + 1);
				CurrentDayOfMonth = 1;

				if (CurrentMonth == Enums.Month.December)
				{
					CurrentMonth = Enums.Month.January;
					CurrentYear += 1;
					monthChanged = true;
					yearChanged = true;
				}
				else
				{
					CurrentMonth = (Enums.Month)((int)CurrentMonth + 1);
					monthChanged = true;
				}
			}
		}

		RecomputeDerivedDateFields();

		EmitSignal(SignalName.DayChanged, CurrentDayOfYear, CurrentDayOfMonth, (int) CurrentDay);
		EmitSignal(SignalName.DateChanged, CurrentYear, (int)CurrentMonth, CurrentDayOfMonth,  (int)CurrentDay);

		if (monthChanged)
		{
			EmitSignal(SignalName.MonthChanged,  (int)CurrentMonth);
		}

		if (yearChanged)
		{
			EmitSignal(SignalName.YearChanged, CurrentYear);
		}
	}

	private void RecomputeDerivedDateFields()
	{
		int doy = 0;
		foreach (var m in MonthOrder)
		{
			if (m == CurrentMonth) break;
			doy += daysInMonth[m];
		}

		CurrentDayOfYear = doy + CurrentDayOfMonth;
	}

	private void UpdateUI()
	{
		if (currentTimeUI == null) return;

		currentTimeUI.Text =
			$"Current Time: {CurrentHour:D2}:{CurrentMinute:D2}:{CurrentSeconds:D2}\n" +
			$"Date: {CurrentMonth} {CurrentDayOfMonth:D2}, {CurrentYear}";
	}

	private void UpdateSunLight(double delta)
	{
		if (sunLight == null) return;

		float day01 = secondsOfDay / (float)SecondsPerDay;
		float targetY = -(day01 * Mathf.Tau) + Mathf.DegToRad(sunTimeOffsetDegrees);

		float speed = sunFollow * Mathf.Max(1, timeSpeed);
		float t = 1f - Mathf.Exp(-speed * (float)delta);

		Vector3 rot = sunLight.Rotation;
		rot.X = Mathf.DegToRad(axialTiltDegrees);
		rot.Y = Mathf.LerpAngle(rot.Y, targetY, t);
		rot.Z = 0f;
		sunLight.Rotation = rot;
	}

	public void SetTimeSpeed(int amount) => timeSpeed = amount;

	public bool TryGetDayOfMonth(int dayOfYear, out int dayOfMonth, out Enums.Month month)
	{
		dayOfMonth = -1;
		month = Enums.Month.January;

		if (dayOfYear < 1 || dayOfYear > 365) return false;

		int remaining = dayOfYear;
		foreach (var m in MonthOrder)
		{
			int dim = daysInMonth[m];
			if (remaining <= dim)
			{
				month = m;
				dayOfMonth = remaining;
				return true;
			}
			remaining -= dim;
		}

		return false;
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);

		if (@event is InputEventKey keyEvent &&
		    keyEvent.Pressed &&
		    keyEvent.Keycode == Key.O)
		{
			GD.Print(
				$"Current Date is {CurrentMonth}/{CurrentDayOfMonth:D2}/{CurrentYear}\n" +
				$"Current Time: {CurrentHour:D2}:{CurrentMinute:D2}:{CurrentSeconds:D2}"
			);
		}
	}
}