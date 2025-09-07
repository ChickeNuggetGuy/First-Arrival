using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class ActionButtonUI : UIElement
{
	public ActionDefinition actionDefinition;
	[Export] Button actionButton;

	protected override async Task _Setup()
	{
		if (actionDefinition == null) return;
		actionButton.Text = actionDefinition.GetActionName();
		actionButton.Pressed += ActionButtonOnPressed; 
	}

	private void ActionButtonOnPressed()
	{
		if (actionDefinition == null)
		{
			GD.Print("ActionButtonOnPressed actionDefinition is null");
			return;
		}
		ActionManager.Instance.SetSelectedAction(actionDefinition);
	}
}
