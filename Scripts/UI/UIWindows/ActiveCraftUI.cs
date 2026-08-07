using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class ActiveCraftUI : UIWindow
{
	[Export] private Control activeCraftHolder;
	[Export] private PackedScene activeCraftButton;

	private GlobeTeamHolder playerTeam;
	private bool craftStateSignalConnected;

	protected override Task _Setup()
	{
		if (activeCraftHolder == null)
		{
			GD.PrintErr("ActiveCraftUI.Setup(): activeCraftHolder is null");
			return Task.CompletedTask;
		}

		if (activeCraftButton == null)
		{
			GD.PrintErr("ActiveCraftUI.Setup(): activeCraftButton is null");
			return Task.CompletedTask;
		}

		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager == null)
		{
			GD.PrintErr("ActiveCraftUI.Setup(): GlobeTeamManager is null");
			return Task.CompletedTask;
		}

		GlobeTeamHolder currentPlayerTeam =
			teamManager.GetTeamData(Enums.UnitTeam.Player);
		if (currentPlayerTeam == null)
		{
			GD.PrintErr("ActiveCraftUI.Setup(): player team is null");
			return Task.CompletedTask;
		}

		if (craftStateSignalConnected && playerTeam != currentPlayerTeam)
		{
			playerTeam.CraftStateChanged -= TeamHolderCraftStateChanged;
			craftStateSignalConnected = false;
		}

		playerTeam = currentPlayerTeam;
		if (!craftStateSignalConnected)
		{
			playerTeam.CraftStateChanged += TeamHolderCraftStateChanged;
			craftStateSignalConnected = true;
		}

		UpdateActiveCraftButtons(playerTeam);
		return Task.CompletedTask;
	}

	public override void _ExitTree()
	{
		if (craftStateSignalConnected && playerTeam != null)
		{
			playerTeam.CraftStateChanged -= TeamHolderCraftStateChanged;
			craftStateSignalConnected = false;
		}

		base._ExitTree();
	}

	private void TeamHolderCraftStateChanged(GlobeTeamHolder teamHolder)
	{
		UpdateActiveCraftButtons(teamHolder);
	}

	private void UpdateActiveCraftButtons(GlobeTeamHolder teamHolder)
	{
		ClearActiveCraftButtons();
		if (teamHolder == null)
		{
			GD.PrintErr("ActiveCraftUI.UpdateActiveCraftButtons(): teamHolder is null");
			return;
		}

		int listIndex = 1;
		foreach (TeamBaseCellDefinition baseDefinition in teamHolder.Bases)
		{
			if (baseDefinition == null)
				continue;

			foreach (Craft craft in baseDefinition.CraftList)
			{
				if (craft == null || craft.Status == Enums.CraftStatus.Home)
					continue;

				CreateActiveCraftButton(craft, listIndex);
				listIndex++;
			}
		}
	}

	private void CreateActiveCraftButton(Craft craft, int listIndex)
	{
		CraftButtonUI craftButtonUi =
			activeCraftButton.Instantiate() as CraftButtonUI;
		if (craftButtonUi == null)
		{
			GD.PrintErr(
				"ActiveCraftUI.CreateActiveCraftButton(): activeCraftButton must instantiate a CraftButtonUI");
			return;
		}

		craftButtonUi.craft = craft;
		craftButtonUi.listIndex = listIndex;
		activeCraftHolder.AddChild(craftButtonUi);
		_ = craftButtonUi.SetupCall();
	}

	private void ClearActiveCraftButtons()
	{
		foreach (Node child in activeCraftHolder.GetChildren())
		{
			activeCraftHolder.RemoveChild(child);
			child.QueueFree();
		}
	}

	protected override Task DrawUI()
	{
		UpdateActiveCraftButtons(playerTeam);
		return Task.CompletedTask;
	}
}
