using Godot;
using System;
using System.Text;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class MainBaseUI : UIWindow
{
	[Export] private CustomLabel currentFundsLabel;
	[Export] private CustomLabel currentDateLabel;
	
	[Export] private LineEdit baseNameEdit;
	[Export] private Button _returnToGlobeButton;
	[Export] private Button _unitDetailsButton;
	[Export] private Button _buySellButton;
	[Export] private Button _craftUiButton;
	[Export] private Button _buildFacilityButton;
	[Export] private Control _facilityBuildOverlay;
	[Export] private VBoxContainer _facilityButtonList;
	[Export] private RichTextLabel _facilityDetailsLabel;
	[Export] private Button _facilityBackButton;
	private Button _researchButton;
	
	[Export] private UnitsPanelUI _unitsPanelUi;
	[Export] private BuySellUI _buySellUi;
	[Export] private EquipCraftUI _equipCraftUi;
	private bool signalsConnected;

	private TeamBaseCellDefinition CurrentBase => GameManager.Instance.currentBase;

	public override void _Ready()
	{
		base._Ready();
		ConnectSignals();
	}

	protected override Task _Setup()
	{
		MissionUITheme.Apply(this, true);
		MissionUITheme.InsetPanelContent(this, 16);
		MissionUITheme.NormalizeButtonText(this);
		StyleBaseName();
		HideEmptyMenuButtons();
		HideFacilityBuildOverlay();
		ConnectSignals();
		
		if (GameManager.Instance != null && GameManager.Instance.currentBase != null)
		{
			baseNameEdit.Text = GameManager.Instance.currentBase.definitionName;
			currentFundsLabel.Text = GameManager.Instance.currentBase.parentTeamHolder.funds.ToString("N0");
		}
			
		return Task.CompletedTask;
	}

	private void StyleBaseName()
	{
		if (baseNameEdit == null) return;
		baseNameEdit.Alignment = HorizontalAlignment.Center;
		baseNameEdit.AddThemeFontSizeOverride("font_size", 22);
		baseNameEdit.AddThemeColorOverride(
			"font_color",
			MissionUITheme.AccentColor.Lightened(0.2f));
	}

	private void HideEmptyMenuButtons()
	{
		VBoxContainer menu = GetNodeOrNull<VBoxContainer>("Panel/VBoxContainer");
		if (menu == null) return;
		foreach (Node child in menu.GetChildren())
		{
			if (child is Button button && string.IsNullOrWhiteSpace(button.Text))
				button.Hide();
		}
	}

	private void ConnectSignals()
	{
		if (signalsConnected) return;
		_researchButton ??= GetNodeOrNull<Button>(
			"Panel/VBoxContainer/ResearchButton");

		if (baseNameEdit != null)
		{
			baseNameEdit.TextChanged += BaseNameEditOnTextChanged;
		}

		if (_returnToGlobeButton != null)
		{
			_returnToGlobeButton.Pressed += ReturnToGlobeButtonOnPressed;
		}
		
		if (_unitDetailsButton != null)
		{
			_unitDetailsButton.Pressed += UnitDetailsButtonOnPressed;
		}
		
		if (_buySellButton != null)
		{
			_buySellButton.Pressed += BuySellButtonOnPressed;
		}

		if (_craftUiButton != null)
		{
			_craftUiButton.Pressed += CraftUiButtonOnPressed;
		}

		if (_buildFacilityButton != null)
		{
			_buildFacilityButton.Pressed += BuildFacilityButtonOnPressed;
		}

		if (_facilityBackButton != null)
		{
			_facilityBackButton.Pressed += HideFacilityBuildOverlay;
		}

		if (_researchButton != null)
		{
			_researchButton.TooltipText =
				"Return to the globe and manage team research.";
			_researchButton.Pressed += ResearchButtonOnPressed;
		}

		signalsConnected = true;
	}

	private void BaseNameEditOnTextChanged(string newText)
	{
		GameManager.Instance.currentBase.definitionName = newText;
		GameManager.Instance.SyncCurrentBaseToGlobeState();
	}

	private void BuildFacilityButtonOnPressed()
	{
		BaseGridManager gridManager = BaseGridManager.Instance;
		if (gridManager == null || !GodotObject.IsInstanceValid(gridManager))
			return;

		gridManager.SetBuildFacilityMode(false);
		RefreshFacilityButtons(gridManager);
		if (_facilityBuildOverlay == null) return;
		_facilityBuildOverlay.Show();
		_facilityBuildOverlay.MouseFilter = MouseFilterEnum.Stop;
	}

	private void RefreshFacilityButtons(BaseGridManager gridManager)
	{
		if (_facilityButtonList == null) return;

		foreach (Node child in _facilityButtonList.GetChildren())
		{
			_facilityButtonList.RemoveChild(child);
			child.QueueFree();
		}

		var options = gridManager.GetAvailableFacilityOptions();
		if (options.Count == 0)
		{
			_facilityButtonList.AddChild(new Button
			{
				Text = "No facilities available",
				Disabled = true,
				CustomMinimumSize = new Vector2(0, 48)
			});
			SetFacilityDetails(
				"There are no additional facilities available for this base.");
			return;
		}

		SetFacilityDetails(
			"Hover over a facility to view its effects, costs, " +
			"construction time, and footprint.");

		foreach (BaseGridManager.FacilityBuildOption option in options)
		{
			BaseGridManager.FacilityBuildOption capturedOption = option;
			string details = BuildFacilityDetails(capturedOption);
			var button = new Button
			{
				Text = capturedOption.Definition.DisplayName,
				Alignment = HorizontalAlignment.Left,
				Disabled = !capturedOption.CanAfford,
				TooltipText = details,
				CustomMinimumSize = new Vector2(0, 48),
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			button.MouseEntered += () => SetFacilityDetails(details);
			button.Pressed += () => SelectFacility(capturedOption);
			_facilityButtonList.AddChild(button);
		}
	}

	private void SelectFacility(BaseGridManager.FacilityBuildOption option)
	{
		BaseGridManager gridManager = BaseGridManager.Instance;
		if (gridManager != null &&
		    GodotObject.IsInstanceValid(gridManager) &&
		    gridManager.BeginFacilityPlacement(option.FacilityKey))
		{
			HideFacilityBuildOverlay();
			return;
		}

		SetFacilityDetails(
			BuildFacilityDetails(option) +
			"\n\nThis facility is no longer available. Reopen the list to refresh it.");
	}

	private static string BuildFacilityDetails(
		BaseGridManager.FacilityBuildOption option)
	{
		FacilityDefinition definition = option.Definition;
		Vector2I gridSize = definition.GetValidatedGridSize();
		string effects = definition.GetEffectsSummary();
		var details = new StringBuilder();
		details.AppendLine(definition.DisplayName);

		if (!string.IsNullOrWhiteSpace(definition.Purpose))
		{
			details.AppendLine();
			details.AppendLine(definition.Purpose.Trim());
		}

		if (!string.IsNullOrWhiteSpace(effects) &&
		    !string.Equals(
			    effects.Trim(),
			    definition.Purpose?.Trim(),
			    StringComparison.Ordinal))
		{
			details.AppendLine();
			details.AppendLine("Effects");
			details.AppendLine(effects.Trim());
		}

		details.AppendLine();
		details.AppendLine($"Upfront cost: ${Mathf.Max(0, definition.InitialCost):N0}");
		details.AppendLine($"Monthly upkeep: ${Mathf.Max(0, definition.MonthlyCost):N0}");
		details.AppendLine($"Build time: {Mathf.Max(0, definition.BuildTimeDays):N0} days");
		details.Append($"Footprint: {gridSize.X} x {gridSize.Y} cells");

		if (!option.CanAfford)
			details.Append("\n\nInsufficient funds.");

		return details.ToString();
	}

	private void SetFacilityDetails(string text)
	{
		if (_facilityDetailsLabel != null)
			_facilityDetailsLabel.Text = text;
	}

	private void HideFacilityBuildOverlay()
	{
		if (_facilityBuildOverlay == null) return;
		_facilityBuildOverlay.Hide();
		_facilityBuildOverlay.MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_facilityBuildOverlay?.Visible != true ||
		    @event is not InputEventKey
		    {
			    Pressed: true,
			    Echo: false,
			    Keycode: Key.Escape
		    })
		{
			return;
		}

		HideFacilityBuildOverlay();
		GetViewport().SetInputAsHandled();
	}

	protected override Task DrawUI()
	{
		currentFundsLabel.Text = GameManager.Instance?.currentBase.parentTeamHolder.funds.ToString("N0");
		return Task.CompletedTask;
	}

	private async void CraftUiButtonOnPressed()
	{
		if (_unitsPanelUi is { IsShown: true })
		{
			await _unitsPanelUi.HideCall();
		}
		
		if (_buySellUi is { IsShown: true })
		{
			await _buySellUi.HideCall();
		}
		
		try
		{
			await _equipCraftUi.Toggle();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to toggle Units Panel: {e.Message}\n{e.StackTrace}");
		}
	}

	private async void UnitDetailsButtonOnPressed()
	{
		
		if (_equipCraftUi is { IsShown: true })
		{
			await _equipCraftUi.HideCall();
		}
		
		if (_buySellUi is { IsShown: true })
		{
			await _buySellUi.HideCall();
		}
		
		try
		{
			await _unitsPanelUi.Toggle();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to toggle Units Panel: {e.Message}\n{e.StackTrace}");
		}
	}

	private async void ReturnToGlobeButtonOnPressed()
	{
		await GameManager.Instance.ReturnToGlobe();
	}

	private async void ResearchButtonOnPressed()
	{
		GameManager gameManager = GameManager.Instance;
		if (gameManager == null) return;
		gameManager.RequestResearchWindowOnGlobe();
		await gameManager.ReturnToGlobe();
		if (gameManager.currentScene != GameManager.GameScene.GlobeScene)
			gameManager.CancelResearchWindowRequest();
	}
	
	
	private async void BuySellButtonOnPressed()
	{
		
		if (_equipCraftUi is { IsShown: true })
		{
			await _equipCraftUi.HideCall();
		}
		
		if (_unitsPanelUi is { IsShown: true })
		{
			await _unitsPanelUi.HideCall();
		}
		
		try
		{
			await _buySellUi.Toggle();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to toggle buySell Panel: {e.Message}\n{e.StackTrace}");
		}
	}

	public override void _ExitTree()
	{
		if (signalsConnected)
		{
			if (baseNameEdit != null)
				baseNameEdit.TextChanged -= BaseNameEditOnTextChanged;
			if (_returnToGlobeButton != null)
				_returnToGlobeButton.Pressed -= ReturnToGlobeButtonOnPressed;
			if (_unitDetailsButton != null)
				_unitDetailsButton.Pressed -= UnitDetailsButtonOnPressed;
			if (_buySellButton != null)
				_buySellButton.Pressed -= BuySellButtonOnPressed;
			if (_craftUiButton != null)
				_craftUiButton.Pressed -= CraftUiButtonOnPressed;
			if (_buildFacilityButton != null)
				_buildFacilityButton.Pressed -= BuildFacilityButtonOnPressed;
			if (_facilityBackButton != null)
				_facilityBackButton.Pressed -= HideFacilityBuildOverlay;
			if (_researchButton != null)
				_researchButton.Pressed -= ResearchButtonOnPressed;
			signalsConnected = false;
		}

		base._ExitTree();
	}
}
