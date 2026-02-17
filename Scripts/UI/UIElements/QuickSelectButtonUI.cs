using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class QuickSelectButtonUI : UIElement
{
	public GridObject TargetGridObject {get; private set;}
	[Export]GridStatBarUI[] statBars = new  GridStatBarUI[0];

	[Export] private Button _button;
	
	public void SetTargetGridObject(GridObject gridObject)
	{
		TargetGridObject = gridObject;
		foreach (GridStatBarUI statBar in statBars)
		{
			statBar.SetupStatBar(gridObject);
		}

	}

	public override void _ExitTree()
	{
		_button.Pressed -= QuickSelectUnit;
		base._ExitTree();
	}

	private void QuickSelectUnit()
	{
		if(TargetGridObject == null)
			return;

		GridObjectManager.Instance.SetCurrentGridObject(TargetGridObject.Team, TargetGridObject);
		CameraController.Instance.FocusOn(TargetGridObject);
	}

	protected override async Task _Setup()
	{
		_button.Pressed += QuickSelectUnit;
	}
}
