using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
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
	[Export] private Label monthlyScoreLabel;
	
	[ExportGroup("Time"), Export] private Label currentDateUI;
	[ExportGroup("Time"), Export] private Dictionary<int, SpeedButtonUI> TimeSpeedButtons;

	[ExportGroup("Bases"), Export] private Control baseButtonHolder;
	private Dictionary<int, Button> baseButtons = new Dictionary<int, Button>();
	protected override Task _Setup()
	{
		
		if (buildBaseButton != null && !buildBaseButton.IsConnected(BaseButton.SignalName.Pressed, Callable.From(BuildBaseButtonOnPressed)))
		{
			buildBaseButton.Pressed += BuildBaseButtonOnPressed;
		}
		
		if (sendMissionButton != null&& !sendMissionButton.IsConnected(BaseButton.SignalName.Pressed, Callable.From(sendMissionButtonOnPressed)))
		{
			sendMissionButton.Pressed += sendMissionButtonOnPressed;
		}
		

		GlobeTimeManager.Instance.DateChanged += TimeManagerOnDateChanged;
		
		
		
		if(!buyCraftButton.IsConnected(BaseButton.SignalName.Pressed, Callable.From(BuyCraftButtonOnPressed)))
			buyCraftButton.Pressed += BuyCraftButtonOnPressed;
		
	
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager != null)
		{
			GD.Print(" found!");
			GlobeTeamHolder teamHolder = teamManager.GetTeamData(Enums.UnitTeam.Player);

			if (teamHolder == null)
			{
				GD.Print("Team Data not found!");
				return Task.CompletedTask;
			}

			currentFundsUI.Text = $"Current Funds: {teamHolder.funds}";
			monthlyScoreLabel.Text = $"Monthly Score: {teamHolder.monthlyScore}";
			teamHolder.FundsChanged += TeamHolderOnFundsChanged;
			teamHolder.MonthlyScoreChanged += TeamHolderOnMonthlyScoreChanged;
			teamHolder.BaseAdded += TeamHolderOnBaseAdded;
			teamHolder.BaseRemoved += TeamHolderOnBaseRemoved;
		}
		else
		{
			GD.Print("not found!");
		}
		RefreshUI();
		return base._Setup();
	}

	private void TeamHolderOnMonthlyScoreChanged(int score)
	{
		monthlyScoreLabel.Text = $"Monthly Score: {score}";
	}


	private void RefreshUI()
	{
		RefreshBaseButtons(GlobeTeamManager.Instance.GetTeamData(Enums.UnitTeam.Player));
	}

	private void RefreshBaseButtons( GlobeTeamHolder teamHolder)
	{
		foreach (Node child in baseButtonHolder.GetChildren())
		{
			child.QueueFree();
		}
		baseButtons.Clear();

		foreach (TeamBaseCellDefinition baseCellDefinition in teamHolder.Bases)
		{
			CreateBaseButton(baseCellDefinition.cellIndex, teamHolder);
		}
		
	}

	private void CreateBaseButton(int cellIndex, GlobeTeamHolder teamHolder)
	{
		if(cellIndex == -1) return;
		for (int i = 0; i < teamHolder.Bases.Count; i++)
		{
			TeamBaseCellDefinition baseCellDefinition = teamHolder.Bases[i];
			if (baseCellDefinition.cellIndex == cellIndex)
			{
				Button baseButton = new Button();
				baseButton.Text = baseCellDefinition.definitionName;
            
				// Mark as async to await the scene change
				baseButton.Pressed += async () => 
				{
					OrbitalCamera.Instance.FocusOnCell(baseCellDefinition.cellIndex);
					GameManager.Instance.currentBase = baseCellDefinition;
                
					SavesManager.Instance.StashSceneState("GlobeState");

					await GameManager.Instance.ChangeSceneAsync(GameManager.GameScene.BaseScene, false);
				};
            
				baseButtonHolder.AddChild(baseButton);
				baseButtons.Add(cellIndex, baseButton);
			}
		}
	}

	#region Signal Listeners
	private void TeamHolderOnBaseRemoved(int hexCellIndex, GlobeTeamHolder teamHolder)
	{
		RefreshBaseButtons(teamHolder);
	}

	private void TeamHolderOnBaseAdded(int hexCellIndex, GlobeTeamHolder teamHolder)
	{
		RefreshBaseButtons(teamHolder);
	}

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
