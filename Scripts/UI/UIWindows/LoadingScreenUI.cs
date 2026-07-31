using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class LoadingScreenUI : UIWindow
{
	[Export] private Label loadingPercentLabel;
	[Export] private ProgressBar loadingBar;
	private bool wasLoading;

	protected override async Task _Setup()
	{
		wasLoading = false;
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

		// Each scene owns its own loading-screen instance. The previous scene's
		// instance is destroyed during ChangeSceneToFile, so the new scene must
		// show its inspector-hidden instance while loading is still in progress.
		if (isLoading && !wasLoading)
		{
			_ = ShowCall(false);
		}
		else if (!isLoading && (wasLoading || Visible))
		{
			_ = HideCall(false);
		}
		wasLoading = isLoading;

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

	}
	
	protected override async Task DrawUI()
	{
	}

}
