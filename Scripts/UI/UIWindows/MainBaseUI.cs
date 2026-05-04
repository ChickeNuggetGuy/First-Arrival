using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

public partial class MainBaseUI : UIWindow
{
	[Export] private Button _returnToGlobeButton;
	[Export] private Button _unitDetailsButton;
	[Export] private UnitsPanelUI _unitsPanelUi;

	protected override async Task _Setup()
	{
		base._Setup();

		if (_returnToGlobeButton != null)
		{
			_returnToGlobeButton.Pressed += ReturnToGlobeButtonOnPressed;
		}
		
		if (_unitDetailsButton != null)
		{
			_unitDetailsButton.Pressed += UnitDetailsButtonOnPressed  ;
		}
	}

	private void UnitDetailsButtonOnPressed()
	{
		if (_unitsPanelUi != null)
		{
			_unitsPanelUi.Toggle();
		}
	}

	private void ReturnToGlobeButtonOnPressed()
	{
		GameManager.ReturnToGlobe();
	}
}
