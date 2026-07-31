using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class MainBaseUI : UIWindow
{
	[Export] private LineEdit baseNameEdit;
	[Export] private Button _returnToGlobeButton;
	[Export] private Button _unitDetailsButton;
	[Export] private Button _buySellButton;
	[Export] private Button _craftUiButton;
	[Export] private Button _buildFacilityButton;
	
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
		ConnectSignals();
		return Task.CompletedTask;
	}

	private void ConnectSignals()
	{
		if (signalsConnected) return;

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

		gridManager.SetBuildFacilityMode(!gridManager.BuildFacilityMode);
	}

	protected override async Task DrawUI()
	{
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
			signalsConnected = false;
		}

		base._ExitTree();
	}
}
