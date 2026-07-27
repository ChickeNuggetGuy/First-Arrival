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
	[Export] private Button continueButton;

	private readonly Dictionary<
		Enums.UnitTeam,
		Godot.Collections.Dictionary<Enums.MonthlyScoreReason, int>> reportScores = new();

	private Enums.Month reportedMonth;
	private int reportedYear;
	private bool wasPausedBeforeReport;
	private bool pausedByReport;

	protected override async Task _Setup()
	{
		ProcessMode = ProcessModeEnum.Always;
		await base._Setup();

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
			reportTitleLabel.Text = $"{reportedMonth} {reportedYear} Score Report";

		if (teamReportsContainer == null)
			return Task.CompletedTask;

		foreach (Node child in teamReportsContainer.GetChildren())
		{
			teamReportsContainer.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var teamReport in reportScores)
			AddTeamReport(teamReport.Key, teamReport.Value);

		return Task.CompletedTask;
	}

	private async void TimeManagerOnMonthChanged(Enums.Month newMonth)
	{
		if (IsShown) return;

		GlobeTimeManager timeManager = GlobeTimeManager.Instance;
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (timeManager == null || teamManager == null) return;

		reportedMonth = newMonth == Enums.Month.January
			? Enums.Month.December
			: (Enums.Month)((int)newMonth - 1);
		reportedYear = newMonth == Enums.Month.January
			? timeManager.CurrentYear - 1
			: timeManager.CurrentYear;

		reportScores.Clear();
		foreach (var team in teamManager.GetAllTeamData())
		{
			if (team.Value == null) continue;
			reportScores[team.Key] = team.Value.GetMonthlyScoreSnapshot();
			team.Value.ResetMonthlyScore();
		}

		SceneTree tree = GetTree();
		wasPausedBeforeReport = tree.Paused;
		tree.Paused = true;
		pausedByReport = true;

		await ShowCall();
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
			AddScoreRow("No score changes", 0);
		}
		else
		{
			foreach (var score in scores)
			{
				total += score.Value;
				string reason = score.Key == Enums.MonthlyScoreReason.None
					? "Other"
					: FormatName(score.Key.ToString());
				AddScoreRow(reason, score.Value);
			}
		}

		AddScoreRow("Total", total, true);
		teamReportsContainer.AddChild(new HSeparator());
	}

	private void AddScoreRow(string reason, int score, bool isTotal = false)
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
		teamReportsContainer.AddChild(row);
	}

	private async void ContinueButtonOnPressed()
	{
		if (!IsShown) return;

		await HideCall();
		RestorePauseState();
		reportScores.Clear();
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
		base._ExitTree();
	}

	private static string FormatScore(int score) =>
		score > 0 ? $"+{score:N0}" : $"{score:N0}";

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
