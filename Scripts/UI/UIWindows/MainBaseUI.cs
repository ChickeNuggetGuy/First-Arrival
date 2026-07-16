using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class MainBaseUI : UIWindow
{
	[Export] private Button _returnToGlobeButton;
	[Export] private Button _unitDetailsButton;
	[Export] private Button _buySellButton;
	[Export] private Button _craftUiButton;
	
	[Export] private UnitsPanelUI _unitsPanelUi;
	[Export] private BuySellUI _buySellUi;
	[Export] private EquipCraftUI _equipCraftUi;
	

	private TeamBaseCellDefinition CurrentBase
	{
		get
		{
			return GameManager.Instance.currentBase;
		}
	}
	
	protected override async Task _Setup()
	{
		await base._Setup();

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
	}

	protected override async Task DrawUI()
	{
	}

	private async void CraftUiButtonOnPressed()
	{
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
		try
		{
			await _buySellUi.Toggle();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to toggle buySell Panel: {e.Message}\n{e.StackTrace}");
		}
	}
}
