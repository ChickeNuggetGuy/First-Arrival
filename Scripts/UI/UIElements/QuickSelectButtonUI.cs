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
	[Export] private Label nameLabel;
	[Export] private TextureRect thumbnail;

	[Export] private Button _button;

	public void SetTargetGridObject(GridObject gridObject)
	{
		TargetGridObject = gridObject;
		foreach (GridStatBarUI statBar in statBars)
		{
			statBar.SetupStatBar(gridObject);
		}

		nameLabel.Text = TargetGridObject.Name;
		_ = UpdateThumbnailAsync(gridObject);

	}

	private async Task UpdateThumbnailAsync(GridObject gridObject)
	{
		Texture2D texture = await gridObject.GetOrCreateThumbnailAsync();

		// The card may have been recycled or removed while the GPU capture ran.
		if (texture == null ||
		    TargetGridObject != gridObject ||
		    !GodotObject.IsInstanceValid(this) ||
		    thumbnail == null)
		{
			return;
		}

		thumbnail.Texture = texture;
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
