using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class QuickSelectButtonUI : UIElement
{
	public GridObject TargetGridObject {get; private set;}

	[Export] private Button _button;
	
	public void SetTargetGridObject(GridObject gridObject)
	{
		TargetGridObject = gridObject;

	}

	private void QuickSelectUnit()
	{
		if(TargetGridObject == null)
			return;

		GridObjectManager.Instance.SetCurrentGridObject(TargetGridObject.Team, TargetGridObject);
	}

	protected override async Task _Setup()
	{
		_button.Pressed += QuickSelectUnit;
	}
}
