using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class MainBaseUI : UIWindow
{
	[Export] private Button _returnToGlobeButton;
	[Export] private Button _unitDetailsButton;
	[Export] private UnitsPanelUI _unitsPanelUi;

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
}
