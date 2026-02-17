using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class LoadingSCcreenUI : UIWindow
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

		if (isLoading && !IsShown)
		{
			_ = ShowCall();
		}
		else if (!isLoading && IsShown)
		{
			_ = HideCall();
		}

		if (isLoading)
		{
			float percent = GameManager.Instance.loadingPercent;
			if (loadingBar != null)
			{
				loadingBar.Value = percent * 100f;
			}
			if (loadingPercentLabel != null)
			{
				loadingPercentLabel.Text = $"{GameManager.Instance.loadingState}: {(percent * 100f):F0}%";
			}

			if (percent >= 1f)
			{
				HideCall();
			}
		}
	}
}
