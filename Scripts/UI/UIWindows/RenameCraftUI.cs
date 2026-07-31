using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class RenameCraftUI : UIWindow
{
	[Export] private LineEdit renameEdit;
	[Export] private Button confirmButton;
	[Export] private Button cancelButton;
	
	[Export] private EquipCraftUI equipCraftUI;

	protected override async Task DrawUI()
	{
		if (equipCraftUI != null && equipCraftUI.currentCraft != null)
		{
			renameEdit.Text = equipCraftUI.currentCraft.GetName();
		}
	}

	protected override async Task _Setup()
	{
		if (renameEdit != null)
		{
			renameEdit.Text = "";
			renameEdit.TextChanged += RenameEditOnTextChanged;
		}

		if (confirmButton != null)
		{
			confirmButton.Pressed += ConfirmButtonOnPressed;
		}

		if (cancelButton != null)
		{
			cancelButton.Pressed += CancelButtonOnPressed;
		}
		
	}

	private void RenameEditOnTextChanged(string newText)
	{
		confirmButton.Disabled = newText == "";
	}

	private void CancelButtonOnPressed()
	{
		_ = HideCall();
	}

	private void ConfirmButtonOnPressed()
	{
		if (equipCraftUI is { currentCraft: not null })
		{
			equipCraftUI.currentCraft.SetName(renameEdit.Text);
			GameManager.Instance.SyncCurrentBaseToGlobeState();
			equipCraftUI.ShowCall();
			_ = HideCall();
		}
	}
}
