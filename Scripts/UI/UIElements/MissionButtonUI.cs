using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class MissionButtonUI : UIElement
{
	[Export] private Button Button;
	public MissionBase mission;
	public int listIndex = -1;
	protected override async Task _Setup()
	{
		if (Button == null)
		{
			GD.PrintErr("MissionButtonUI.Setup(): Button is null");
			return;
		}

		Button.Text = listIndex.ToString();
		
		if(!Button.IsConnected(Button.SignalName.Pressed, Callable.From(ButtonOnPressed)))
			Button.Pressed += ButtonOnPressed;
	}


	private void ButtonOnPressed()
	{
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager.SendCraftMode)
		{
			GlobeTeamHolder playerTeamHolder = teamManager.GetTeamData(Enums.UnitTeam.Player);
				
			if (playerTeamHolder.SelectedCraft != null)
			{
				var selectedCraft = playerTeamHolder.SelectedCraft;
				TeamBaseCellDefinition baseDef = selectedCraft.GetBaseCellDefinition();
					
				if (baseDef == null)
				{
					foreach(var b in playerTeamHolder.Bases)
					{
						if (b.TryGetCraftFromIndex(selectedCraft.Index, out _))
						{
							baseDef = b;
							selectedCraft.SetBaseCellDefinition(b);
							break;
						}
					}
				}

				if (baseDef != null)
				{
					GD.Print("Send Craft Command Issued");
					_ = baseDef.SendCraft(selectedCraft.CurrentCellIndex, mission.cellIndex, selectedCraft, teamManager);
					teamManager.SetSendCraftMode(false, teamManager.GetTeamData(Enums.UnitTeam.Player), null);
				}
				else
				{
					GD.PrintErr("Could not find Base Definition for selected craft.");
				}
			}
		}
		else
		{
			GD.Print($"Button.OnPressed(): {mission.cellIndex}");
			_ = OrbitalCamera.Instance.FocusOnCell(mission.cellIndex);
		}
	}
}
