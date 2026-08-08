using Godot;
using System;

public partial class HexCellNotification : NotificationElement
{
	protected int targetHexCellIndex = -1;

	public HexCellNotification(string text, float duration, int targetHexCellIndex) : base(text, duration)
	{
		this.targetHexCellIndex = targetHexCellIndex;
	}

	protected override void OnClicked()
	{
		if (targetHexCellIndex < 0 || OrbitalCamera.Instance == null) return;
		_ = OrbitalCamera.Instance.FocusOnCell(targetHexCellIndex);
	}
}
