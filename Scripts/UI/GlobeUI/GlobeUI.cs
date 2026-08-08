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
	[Export] private Button researchButton;
	[Export] private Button monthlyReportButton;
	[Export] private MonthlyReportUI monthlyReportUI;
	[Export] private SelectCraftUI selectCraftUI;
	[Export] private Label monthlyScoreLabel;
	
	[ExportGroup("Time"), Export] private Label currentDateUI;
	[ExportGroup("Time"), Export] private Dictionary<int, SpeedButtonUI> TimeSpeedButtons;

	[ExportGroup("Bases"), Export] private Control baseButtonHolder;
	[ExportGroup("Bases"), Export] private Texture2D focusButtonTexture;
	private Dictionary<int, HBoxContainer> baseButtons = new Dictionary<int, HBoxContainer>();
	private ResearchWindowUI researchWindow;
	private GameManager researchRequestManager;
	private bool isOpeningBase;
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
		
		

		if (researchButton != null && !researchButton.IsConnected(
			Button.SignalName.Pressed,
			Callable.From(ResearchButtonOnPressed)))
		{
			researchButton.Pressed += ResearchButtonOnPressed;
		}

		if (monthlyReportButton != null && !monthlyReportButton.IsConnected(
			Button.SignalName.Pressed,
			Callable.From(MonthlyReportButtonOnPressed)))
		{
			monthlyReportButton.Pressed += MonthlyReportButtonOnPressed;
		}
		
	
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
			UpdateMonthlyScoreLabel(teamHolder.TotalMonthlyScore);
			teamHolder.FundsChanged += TeamHolderOnFundsChanged;
			teamHolder.MonthlyScoreChanged += TeamHolderOnMonthlyScoreChanged;
			teamHolder.BaseAdded += TeamHolderOnBaseAdded;
			teamHolder.BaseRemoved += TeamHolderOnBaseRemoved;

			if(teamHolder.Bases.Count == 0)
			{
				if (sendMissionButton != null) sendMissionButton.Disabled = true;
				researchButton.Disabled = true;
			}
			else
			{
				if (sendMissionButton != null) sendMissionButton.Disabled = false;
				researchButton.Disabled = false;
			}

		}
		else
		{
			GD.Print("not found!");
		}
		_ = DrawUI();
		GameManager gameManager = GameManager.Instance;
		if (gameManager?.ConsumeResearchWindowRequest() == true)
			QueueRequestedResearchWindow(gameManager);
		return Task.CompletedTask;
	}

	private void QueueRequestedResearchWindow(GameManager gameManager)
	{
		if (gameManager.loadingState == GameManager.LoadingState.NONE)
		{
			_ = OpenRequestedResearchWindowAfterLoading();
			return;
		}

		if (researchRequestManager == gameManager)
			return;

		DisconnectResearchLoadSignal();
		researchRequestManager = gameManager;
		researchRequestManager.CoreManagersLoaded +=
			GameManagerOnCoreManagersLoaded;
	}

	private void GameManagerOnCoreManagersLoaded()
	{
		DisconnectResearchLoadSignal();
		_ = OpenRequestedResearchWindowAfterLoading();
	}

	private async Task OpenRequestedResearchWindowAfterLoading()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (IsInsideTree() &&
		    GameManager.Instance?.loadingState == GameManager.LoadingState.NONE)
		{
			ResearchButtonOnPressed();
		}
	}

	private void DisconnectResearchLoadSignal()
	{
		if (researchRequestManager != null &&
		    GodotObject.IsInstanceValid(researchRequestManager))
		{
			researchRequestManager.CoreManagersLoaded -=
				GameManagerOnCoreManagersLoaded;
		}
		researchRequestManager = null;
	}

	public override void _ExitTree()
	{
		DisconnectResearchLoadSignal();
		base._ExitTree();
	}

	private void TeamHolderOnMonthlyScoreChanged(
		Dictionary<Enums.MonthlyScoreReason, int> score)
	{
		int total = 0;
		foreach (int amount in score.Values)
			total += amount;
		UpdateMonthlyScoreLabel(total);
	}

	private void UpdateMonthlyScoreLabel(int score)
	{
		if (monthlyScoreLabel != null)
			monthlyScoreLabel.Text = $"Monthly Score: {score:N0}";
	}

	protected override Task DrawUI()
	{
		RefreshBaseButtons(GlobeTeamManager.Instance.GetTeamData(Enums.UnitTeam.Player));
		return Task.CompletedTask;
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
				HBoxContainer container = new HBoxContainer();
				baseButtonHolder.AddChild(container);
				
				Button baseButton = new Button();
				baseButton.Text = baseCellDefinition.definitionName;
				
				
				baseButton.Pressed += () =>
					_ = OpenBase(baseCellDefinition.cellIndex);
				
				Button panButton = new Button();
				panButton.Icon = focusButtonTexture;
				panButton.Pressed += async() =>
				{
					await OrbitalCamera.Instance.FocusOnCell(baseCellDefinition.cellIndex);
				};
				
				container.AddChild(baseButton);
				baseButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				container.AddChild(panButton);
				baseButtons.Add(cellIndex, container);
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

		if (teamHolder.Bases.Count > 0)
		{
			sendMissionButton.Disabled = false;
			researchButton.Disabled = false;
		}
		else
		{
			sendMissionButton.Disabled = true;
			researchButton.Disabled = true;
		}
	}

	private void sendMissionButtonOnPressed()
	{
		selectCraftUI.ShowCall();
	}
	
	private void ResearchButtonOnPressed()
	{
		OpenResearchWindow();
	}

	public void OpenResearchWindow()
	{
		GlobeTeamHolder teamHolder = GlobeTeamManager.Instance?.GetTeamData(
			Enums.UnitTeam.Player);
		if (teamHolder == null) return;

		if (researchWindow == null || !GodotObject.IsInstanceValid(researchWindow))
		{
			researchWindow = new ResearchWindowUI { Name = "ResearchWindow" };
			Control windowParent = GetParent() as Control ?? this;
			windowParent.AddChild(researchWindow);
		}

		researchWindow.ShowFor(teamHolder);
	}

	public async Task OpenBase(int baseCellIndex)
	{
		if (isOpeningBase) return;

		GameManager gameManager = GameManager.Instance;
		GlobeTeamManager globeTeamManager = GlobeTeamManager.Instance;
		OrbitalCamera camera = OrbitalCamera.Instance;
		SavesManager savesManager = SavesManager.Instance;
		if (gameManager == null || globeTeamManager == null ||
			camera == null || savesManager == null ||
			gameManager.loadingState != GameManager.LoadingState.NONE)
		{
			return;
		}

		GameManager.GameScene sceneAtStart = gameManager.currentScene;
		if (sceneAtStart != GameManager.GameScene.GlobeScene &&
			sceneAtStart != GameManager.GameScene.NONE)
		{
			return;
		}

		GlobeTeamHolder playerTeam = globeTeamManager.GetTeamData(
			Enums.UnitTeam.Player);
		if (playerTeam == null ||
			!playerTeam.TryGetBaseAtIndex(
				baseCellIndex,
				out TeamBaseCellDefinition targetBase))
		{
			GD.PushWarning($"Base at cell {baseCellIndex} no longer exists.");
			return;
		}

		isOpeningBase = true;
		try
		{
			await camera.FocusOnCell(baseCellIndex);

			// Camera focus awaits several frames. Another navigation may have won
			// during that time, so revalidate both the scene and target base before
			// changing any persistent transition state.
			if (!IsInsideTree() ||
				!GodotObject.IsInstanceValid(gameManager) ||
				gameManager.loadingState != GameManager.LoadingState.NONE ||
				gameManager.currentScene != sceneAtStart)
			{
				return;
			}

			globeTeamManager = GlobeTeamManager.Instance;
			playerTeam = globeTeamManager?.GetTeamData(Enums.UnitTeam.Player);
			if (playerTeam == null ||
				!playerTeam.TryGetBaseAtIndex(baseCellIndex, out targetBase))
			{
				return;
			}

			gameManager.SetCurrentBase(targetBase, playerTeam);
			gameManager.SetCurrentTeamResearchState(
				playerTeam.GetUnlockedItemIdsSnapshot(),
				playerTeam.GetCompletedResearchProjectIdsSnapshot());
			savesManager.StashSceneState("GlobeState");

			await gameManager.ChangeSceneAsync(
				GameManager.GameScene.BaseScene,
				false);
		}
		catch (Exception exception)
		{
			GD.PushError($"Failed to open base: {exception.Message}");
		}
		finally
		{
			isOpeningBase = false;
		}
	}

	private void MonthlyReportButtonOnPressed()
	{
		if (monthlyReportUI != null)
			_ = monthlyReportUI.ShowLatestReport();
	}
	
	private void TimeManagerOnDateChanged(int year, Enums.Month month, int date, Enums.Day day)
	{
		currentDateUI.Text = $"Current Time: {month}, {date},{year}";
	}

	private void TeamHolderOnFundsChanged(GlobeTeamHolder teamHolder, long currentFunds)
	{
		GD.Print("Team funds changed: " + teamHolder.funds);
		currentFundsUI.Text = $"Current Funds: {teamHolder.funds}";

		if (currentFunds < teamHolder.newbaseCost)
		{
			buildBaseButton.Disabled = true;
		}
		else
		{
			buildBaseButton.Disabled = false;
		}
	}


	private void BuildBaseButtonOnPressed()
	{
		GlobeTeamManager baseManager = GlobeTeamManager.Instance;
		if (baseManager == null)
		{
			GD.Print($"Base Manager not found");
			return;
		}
		
		baseManager.SetBuildBaseMode(!baseManager.buildBaseMode);
		GD.Print($"Build Base Mode set to {baseManager.buildBaseMode}");
	}

	#endregion
	
}
