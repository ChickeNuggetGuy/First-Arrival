using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

public partial class MonthlyReportUI : UIWindow
{
	[Export] private Label reportTitleLabel;
	[Export] private VBoxContainer teamReportsContainer;
	[Export] private VBoxContainer financeReportsContainer;
	[Export] private VBoxContainer countryReportsContainer;
	[Export] private Button continueButton;

	private readonly Dictionary<
		Enums.UnitTeam,
		Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>> reportScores = new();
	private readonly Dictionary<Enums.UnitTeam, MonthlyFinanceSnapshot>
		reportFinances = new();
	private readonly List<CountryFundingReportEntry> reportCountries = new();

	private Enums.Month reportedMonth;
	private int reportedYear;
	private bool wasPausedBeforeReport;
	private bool pausedByReport;
	private bool reportPending;
	private bool showingCurrentReport;

	protected override async Task _Setup()
	{
		ProcessMode = ProcessModeEnum.Always;

		if (continueButton != null &&
		    !continueButton.IsConnected(
			    Button.SignalName.Pressed,
			    Callable.From(ContinueButtonOnPressed)))
		{
			continueButton.Pressed += ContinueButtonOnPressed;
		}

		if (GlobeTimeManager.Instance != null &&
		    !GlobeTimeManager.Instance.IsConnected(
			    GlobeTimeManager.SignalName.MonthChanged,
			    Callable.From<Enums.Month>(TimeManagerOnMonthChanged)))
		{
			GlobeTimeManager.Instance.MonthChanged += TimeManagerOnMonthChanged;
		}
	}

	protected override Task DrawUI()
	{
		if (reportTitleLabel != null)
			reportTitleLabel.Text = showingCurrentReport
				? $"{reportedMonth} {reportedYear} Monthly Report (Current)"
				: $"{reportedMonth} {reportedYear} Monthly Report";
		if (continueButton != null)
			continueButton.Text = showingCurrentReport ? "Close" : "Continue";

		ClearContainer(teamReportsContainer);
		ClearContainer(financeReportsContainer);
		ClearContainer(countryReportsContainer);

		if (teamReportsContainer != null)
		{
			foreach (var teamReport in reportScores)
				AddTeamReport(teamReport.Key, teamReport.Value);
		}

		if (financeReportsContainer != null && reportFinances.TryGetValue(
			Enums.UnitTeam.Player,
			out MonthlyFinanceSnapshot playerFinances))
		{
			AddFinanceReport(Enums.UnitTeam.Player, playerFinances);
		}

		AddCountryReports();

		return Task.CompletedTask;
	}

	private static void ClearContainer(VBoxContainer container)
	{
		if (container == null) return;
		foreach (Node child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}
	}

	private async void TimeManagerOnMonthChanged(Enums.Month newMonth)
	{
		if (IsShown || reportPending) return;
		reportPending = true;
		// Let all monthly gameplay handlers apply income and upkeep before the UI
		// snapshots and resets the completed month's ledgers.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (IsShown)
		{
			reportPending = false;
			return;
		}

		GlobeTimeManager timeManager = GlobeTimeManager.Instance;
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (timeManager == null || teamManager == null)
		{
			reportPending = false;
			return;
		}

		reportedMonth = newMonth == Enums.Month.January
			? Enums.Month.December
			: (Enums.Month)((int)newMonth - 1);
		reportedYear = newMonth == Enums.Month.January
			? timeManager.CurrentYear - 1
			: timeManager.CurrentYear;
		showingCurrentReport = false;

		CaptureTeamReports(teamManager, resetMonthlyLedgers: true);
		reportCountries.Clear();
		reportCountries.AddRange(
			teamManager.GetLatestCompletedCountryFundingReport());

		PauseForReport();

		await ShowCall();
		reportPending = false;
	}

	public async Task ShowLatestReport()
	{
		if (IsShown || reportPending) return;
		reportPending = true;

		GlobeTimeManager timeManager = GlobeTimeManager.Instance;
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (timeManager == null || teamManager == null)
		{
			reportPending = false;
			return;
		}

		reportedMonth = timeManager.CurrentMonth;
		reportedYear = timeManager.CurrentYear;
		showingCurrentReport = true;
		CaptureTeamReports(teamManager, resetMonthlyLedgers: false);
		reportCountries.Clear();
		reportCountries.AddRange(teamManager.GetCurrentCountryFundingReport());
		PauseForReport();

		await ShowCall();
		reportPending = false;
	}

	private void CaptureTeamReports(
		GlobeTeamManager teamManager,
		bool resetMonthlyLedgers)
	{
		reportScores.Clear();
		reportFinances.Clear();
		foreach (var team in teamManager.GetAllTeamData())
		{
			if (team.Value == null) continue;
			reportScores[team.Key] = team.Value.GetMonthlyScoreSnapshot();
			reportFinances[team.Key] = team.Value.GetMonthlyFinanceSnapshot();
			if (!resetMonthlyLedgers) continue;
			team.Value.ResetMonthlyScore();
			team.Value.ResetMonthlyFinances();
		}
	}

	private void PauseForReport()
	{
		SceneTree tree = GetTree();
		wasPausedBeforeReport = tree.Paused;
		tree.Paused = true;
		pausedByReport = true;
	}

	private void AddTeamReport(
		Enums.UnitTeam team,
		Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int> scores)
	{
		var teamLabel = new Label
		{
			Text = $"{FormatName(team.ToString())} Team",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		teamLabel.AddThemeFontSizeOverride("font_size", 20);
		teamReportsContainer.AddChild(teamLabel);

		int total = 0;
		if (scores.Count == 0)
		{
				AddScoreRow(teamReportsContainer, "No score changes", 0);
		}
		else
		{
			foreach (var score in scores)
			{
				total += score.Value;
				string reason = score.Key == Enums.MonthlyScoreReason.None
					? "Other"
					: FormatName(score.Key.ToString());
					AddScoreRow(teamReportsContainer, reason, score.Value);
			}
		}

		AddScoreRow(teamReportsContainer, "Total", total, true);
		teamReportsContainer.AddChild(new HSeparator());
	}

	private static void AddScoreRow(
		VBoxContainer container,
		string reason,
		int score,
		bool isTotal = false)
	{
		var row = new HBoxContainer();
		var reasonLabel = new Label
		{
			Text = reason,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		var scoreLabel = new Label
		{
			Text = FormatScore(score),
			HorizontalAlignment = HorizontalAlignment.Right
		};

		if (isTotal)
		{
			reasonLabel.AddThemeFontSizeOverride("font_size", 18);
			scoreLabel.AddThemeFontSizeOverride("font_size", 18);
		}

		row.AddChild(reasonLabel);
		row.AddChild(scoreLabel);
		container.AddChild(row);
	}

	private void AddFinanceReport(
		Enums.UnitTeam team,
		MonthlyFinanceSnapshot finances)
	{
		var teamLabel = new Label
		{
			Text = $"{FormatName(team.ToString())} Team Finances",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		teamLabel.AddThemeFontSizeOverride("font_size", 20);
		financeReportsContainer.AddChild(teamLabel);

		AddFinanceSection("Income", finances.Income, finances.TotalIncome);
		AddFinanceSection(
			"Expenditure",
			finances.Expenditure,
			finances.TotalExpenditure);
		financeReportsContainer.AddChild(new HSeparator());
		AddMoneyRow(
			financeReportsContainer,
			"Net change",
			finances.NetChange,
			true,
			showSign: true);
	}

	private void AddFinanceSection(
		string title,
		IReadOnlyDictionary<string, long> entries,
		long total)
	{
		var heading = new Label { Text = title };
		heading.AddThemeFontSizeOverride("font_size", 18);
		financeReportsContainer.AddChild(heading);

		if (entries.Count == 0)
			AddMoneyRow(financeReportsContainer, $"No {title.ToLowerInvariant()}", 0);
		else
		{
			foreach (var entry in entries)
				AddMoneyRow(financeReportsContainer, entry.Key, entry.Value);
		}

		AddMoneyRow(financeReportsContainer, $"Total {title}", total, true);
		financeReportsContainer.AddChild(new HSeparator());
	}

	private static void AddMoneyRow(
		VBoxContainer container,
		string reason,
		long amount,
		bool isTotal = false,
		bool showSign = false)
	{
		var row = new HBoxContainer();
		var reasonLabel = new Label
		{
			Text = reason,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		var amountLabel = new Label
		{
			Text = showSign && amount > 0
				? $"+${amount:N0}"
				: amount < 0
					? $"-${Math.Abs(amount):N0}"
					: $"${amount:N0}",
			HorizontalAlignment = HorizontalAlignment.Right
		};

		if (isTotal)
		{
			reasonLabel.AddThemeFontSizeOverride("font_size", 18);
			amountLabel.AddThemeFontSizeOverride("font_size", 18);
		}

		row.AddChild(reasonLabel);
		row.AddChild(amountLabel);
		container.AddChild(row);
	}

	private void AddCountryReports()
	{
		if (countryReportsContainer == null) return;
		var namedCountries = reportCountries.FindAll(country =>
			!string.IsNullOrWhiteSpace(country.CountryName) &&
			!country.CountryName.StartsWith(
				"Unnamed",
				StringComparison.OrdinalIgnoreCase));
		if (namedCountries.Count == 0)
		{
			countryReportsContainer.AddChild(new Label
			{
				Text = "No country funding data is available.",
				HorizontalAlignment = HorizontalAlignment.Center
			});
			return;
		}

		var heading = new Label
		{
			Text = showingCurrentReport
				? "Country Financial Support (Current Projection)"
				: "Country Financial Support",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		heading.AddThemeFontSizeOverride("font_size", 20);
		countryReportsContainer.AddChild(heading);

		var grid = new GridContainer
		{
			Columns = 4,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		AddCountryCell(grid, "Country", isHeader: true, expand: true);
		AddCountryCell(grid, "Opinion", isHeader: true);
		AddCountryCell(grid, "Monthly Support", isHeader: true);
		AddCountryCell(grid, "Change This Month", isHeader: true);

		foreach (CountryFundingReportEntry country in namedCountries)
		{
			AddCountryCell(grid, country.CountryName, expand: true);
			AddCountryCell(grid, $"{country.PlayerOpinion:N1}");
			AddCountryCell(grid, FormatMoney(country.MonthlySupport));
			AddCountryCell(
				grid,
				FormatMoneyChange(country.SupportChange),
				change: country.SupportChange);
		}

		countryReportsContainer.AddChild(grid);
	}

	private static void AddCountryCell(
		GridContainer grid,
		string text,
		bool isHeader = false,
		bool expand = false,
		long change = 0)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = expand
				? HorizontalAlignment.Left
				: HorizontalAlignment.Right,
			SizeFlagsHorizontal = expand
				? SizeFlags.ExpandFill
				: SizeFlags.ShrinkEnd,
			CustomMinimumSize = expand
				? new Vector2(220, 0)
				: new Vector2(120, 0)
		};

		if (isHeader)
			label.AddThemeFontSizeOverride("font_size", 16);
		else if (change > 0)
			label.AddThemeColorOverride("font_color", new Color(0.45f, 0.9f, 0.55f));
		else if (change < 0)
			label.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.45f));

		grid.AddChild(label);
	}

	private async void ContinueButtonOnPressed()
	{
		if (!IsShown) return;

		await HideCall();
		RestorePauseState();
		reportScores.Clear();
		reportFinances.Clear();
		reportCountries.Clear();
	}

	private void RestorePauseState()
	{
		if (!pausedByReport) return;
		GetTree().Paused = wasPausedBeforeReport;
		pausedByReport = false;
	}

	public override void _ExitTree()
	{
		if (GlobeTimeManager.Instance != null &&
		    GlobeTimeManager.Instance.IsConnected(
			    GlobeTimeManager.SignalName.MonthChanged,
			    Callable.From<Enums.Month>(TimeManagerOnMonthChanged)))
		{
			GlobeTimeManager.Instance.MonthChanged -= TimeManagerOnMonthChanged;
		}

		if (continueButton != null &&
		    continueButton.IsConnected(
			    Button.SignalName.Pressed,
			    Callable.From(ContinueButtonOnPressed)))
		{
			continueButton.Pressed -= ContinueButtonOnPressed;
		}

		RestorePauseState();
		reportPending = false;
		base._ExitTree();
	}

	private static string FormatScore(int score) =>
		score > 0 ? $"+{score:N0}" : $"{score:N0}";

	private static string FormatMoney(long amount) =>
		amount < 0 ? $"-${Math.Abs(amount):N0}" : $"${amount:N0}";

	private static string FormatMoneyChange(long amount) =>
		amount > 0
			? $"+${amount:N0}"
			: amount < 0
				? $"-${Math.Abs(amount):N0}"
				: "$0";

	private static string FormatName(string value)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;

		var result = new StringBuilder(value.Length + 4);
		result.Append(value[0]);
		for (int i = 1; i < value.Length; i++)
		{
			if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
				result.Append(' ');
			result.Append(value[i]);
		}
		return result.ToString();
	}
}
