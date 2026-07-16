using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class LoadingScreenUI : UIWindow
{
	[Export] private Label loadingPercentLabel;
	[Export] private ProgressBar loadingBar;

	protected override async Task _Setup()
	{
		await base._Setup();
		// Ensure it's hidden or shown based on current loading state
		UpdateUI();
	}

	public override void _Process(double delta)
	{
		UpdateUI();
	}

	private void UpdateUI()
	{
		if (GameManager.Instance == null) return;

		bool isLoading = GameManager.Instance.loadingState != GameManager.LoadingState.NONE;

		// if (isLoading && !IsShown)
		// {
		// 	_ = ShowCall();
		// }
		if (!isLoading && IsShown)
		{
			_ = HideCall();
		}

		if (!isLoading) return;

		float percent = GameManager.Instance.loadingPercent;

		if (loadingBar != null)
		{
			loadingBar.Value = percent * 100f;
		}

		if (loadingPercentLabel != null)
		{
			string state = GameManager.Instance.loadingState.ToString();
			string mgr = GameManager.Instance.loadingManagerName ?? "";
			string mgrPart = string.IsNullOrWhiteSpace(mgr) ? "" : $" - {mgr}";
			loadingPercentLabel.Text = $"{state}{mgrPart}: {(percent * 100f):F0}%";
		}

		if (percent >= 1f)
		{
			_ =HideCall();
		}
	}
	
	protected override async Task DrawUI()
	{
	}

}