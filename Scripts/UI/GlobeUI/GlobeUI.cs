using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class GlobeUI : UIWindow
{
	[Export] private Label currentFundsUI;
	[Export] private Button buildBaseButton;
	[Export] private Button sendMissionButton;
	[Export] private Button buyCraftButton;
	[Export] private SelectCraftUI selectCraftUI;
	
	[ExportGroup("Time"), Export] private Label currentDateUI;
	[ExportGroup("Time"), Export] private Dictionary<int, SpeedButtonUI> TimeSpeedButtons;
	

	protected override Task _Setup()
	{
		
		if (buildBaseButton != null && !buildBaseButton.IsConnected(Button.SignalName.Pressed, Callable.From(BuildBaseButtonOnPressed)))
		{
			buildBaseButton.Pressed += BuildBaseButtonOnPressed;
		}
		
		if (sendMissionButton != null&& !sendMissionButton.IsConnected(Button.SignalName.Pressed, Callable.From(sendMissionButtonOnPressed)))
		{
			sendMissionButton.Pressed += sendMissionButtonOnPressed;
		}
		
		GlobeTimeManager.Instance.DateChanged += TimeManagerOnDateChanged;
		
		
		if(!buyCraftButton.IsConnected(BaseButton.SignalName.Pressed, Callable.From(BuyCraftButtonOnPressed)))
			buyCraftButton.Pressed += BuyCraftButtonOnPressed;
		
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager != null)
		{
			GlobeTeamHolder teamHolder = teamManager.GetTeamData(Enums.UnitTeam.Player);

			if (teamHolder == null)
			{
				GD.Print("Team Data not found!");
				return Task.CompletedTask;
			}
			
			currentFundsUI.Text = $"Current Funds: {teamHolder.funds}";
			teamHolder.FundsChanged += TeamHolderOnFundsChanged;
			
		}
		
		return base._Setup();
	}


	#region Signal Listeners
	private void sendMissionButtonOnPressed()
	{
		selectCraftUI.ShowCall();
	}
	
	private void BuyCraftButtonOnPressed()
	{
		GlobeTeamManager baseManager = GlobeTeamManager.Instance;
		if (baseManager == null)
		{
			GD.Print($"Base Manager not found");
			return;
		}
		
		baseManager.buyCraftMode = !baseManager.buyCraftMode;
	}
	
	private void TimeManagerOnDateChanged(int year, Enums.Month month, int date, Enums.Day day)
	{
		currentDateUI.Text = $"Current Time: {month}, {date},{year}";
	}

	private void TeamHolderOnFundsChanged(GlobeTeamHolder teamHolder, int currentFunds)
	{
		GD.Print("Team funds changed: " + teamHolder.funds);
		currentFundsUI.Text = $"Current Funds: {teamHolder.funds}";
	}


	private void BuildBaseButtonOnPressed()
	{
		GlobeTeamManager baseManager = GlobeTeamManager.Instance;
		if (baseManager == null)
		{
			GD.Print($"Base Manager not found");
			return;
		}
		
		baseManager.buildBaseMode = !baseManager.buildBaseMode;
		GD.Print($"Build Base Mode set to {baseManager.buildBaseMode}");
	}
	
	


	#endregion
	
}
