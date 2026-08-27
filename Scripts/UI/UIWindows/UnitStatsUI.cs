using Godot;
using System;
using System.Threading.Tasks;

[GlobalClass]
public partial class UnitStatsUI : UIWindow
{

	protected override async Task DrawUI()
	{
	}

	protected override async Task _Setup()
	{
		MissionUITheme.Apply(this, true);
		MissionUITheme.InsetPanelContent(this);
		MissionUITheme.StyleFirstTitle(this, 24);
		MissionUITheme.NormalizeButtonText(this);
	}
}
