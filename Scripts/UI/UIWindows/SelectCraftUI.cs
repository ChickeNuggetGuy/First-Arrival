using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class SelectCraftUI : UIWindow
{
	[Export] private Tree treeUI;
	[Export] private PackedScene CraftButtonScene;
	[Export] private Button AcceptButton;
	[Export] private Texture2D buttonTexture;
	private int _destinationCellIndex = -1;

	/// <summary>
	/// Opens the picker and sends the chosen craft directly to the supplied cell.
	/// Passing -1 retains the existing choose-a-craft-then-click-a-destination flow.
	/// </summary>
	public void ShowForDestination(int cellIndex)
	{
		bool requiresDeployableUnits = IsMissionDestination(cellIndex);
		if (GlobeTeamManager.Instance?.HasAvailableCraft(
			    Enums.UnitTeam.Player,
			    requiresDeployableUnits) != true)
		{
			_destinationCellIndex = -1;
			return;
		}

		_destinationCellIndex = cellIndex;
		_ = ShowCall();
	}

	protected override Task _Setup()
	{
		MissionUITheme.Apply(this);
		MissionUITheme.InsetPanelContent(this);
		MissionUITheme.StyleFirstTitle(this);
		treeUI.HideRoot = true;
		if(!AcceptButton.IsConnected(BaseButton.SignalName.Pressed, Callable.From(AcceptButtonOnPressed)))
		{
			AcceptButton.Pressed += AcceptButtonOnPressed;
		}
		
		treeUI.ButtonClicked += TreeUIOnButtonClicked;
		return Task.CompletedTask;
		
	}

	public override void _ExitTree()
	{
		AcceptButton.Pressed -= AcceptButtonOnPressed;
		treeUI.ButtonClicked -= TreeUIOnButtonClicked;
		base._ExitTree();
	}

	
	protected override Task DrawUI()
	{
		SetupTree();
		base._Show();
		return Task.CompletedTask;
	}

	#region Signal Listeners

	private void AcceptButtonOnPressed()
	{
		// TreeItem selectedItem = treeUI.GetSelected();
		//
		// if(selectedItem == null) return;
		//
		// if (selectedItem.Get)

	}


	#endregion


	private void SetupTree()
	{
		if (treeUI == null)
		{
			GD.PrintErr("TreeUI is null");
			return;
		}
		
		
		
		treeUI.Clear();
		
		

		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager == null)
		{
			GD.PrintErr("TeamManager is null");
			return;
		}

		GlobeTeamHolder teamHolder = teamManager.GetTeamData(Enums.UnitTeam.Player);
		if (teamHolder == null)
		{
			GD.PrintErr("TeamHolder is null");
			return;
		}

		var root = treeUI.CreateItem();
		bool requiresDeployableUnits = IsMissionDestination(
			_destinationCellIndex);
		foreach (var teamHolderBase in teamHolder.Bases)
		{
			if (teamHolderBase == null) continue;
			var treeChild = treeUI.CreateItem(root, teamHolderBase.cellIndex);
			treeChild.SetText(0, teamHolderBase.definitionName);

			foreach (var craft in teamHolderBase.CraftList)
			{
				if (craft == null ||
				    craft.Status == Enums.CraftStatus.None ||
				    (requiresDeployableUnits && !craft.HasDeployableUnits))
					continue;

				var treeSubChild = treeUI.CreateItem(treeChild, craft.Index);
				treeSubChild.SetText(0, $"{craft.ItemName} ({craft.Status})");

				treeSubChild.AddButton(0, buttonTexture,craft.Index);
				
			}
		}
	}

	private void TreeUIOnButtonClicked(TreeItem item, long column, long id, long mouseButtonIndex)
	{
		GlobeTeamManager teamManager = GlobeTeamManager.Instance;
		if (teamManager == null)
		{
			GD.PrintErr("TeamManager is null");
			return;
		}

		GlobeTeamHolder teamHolder = teamManager.GetTeamData(Enums.UnitTeam.Player);
		if (teamHolder == null)
		{
			GD.PrintErr("TeamHolder is null");
			return;
		}

		TreeItem baseItem = item.GetParent();
    

		if (baseItem == null || baseItem == treeUI.GetRoot()) 
		{
			baseItem = item; 
		}
		
		int baseListIndex = baseItem.GetIndex();
		
		if (baseListIndex < 0 || baseListIndex >= teamHolder.Bases.Count)
		{
			GD.PrintErr($"Base index {baseListIndex} out of range.");
			return;
		}

		TeamBaseCellDefinition teamBase = teamHolder.Bases[baseListIndex];
		if (!teamBase.TryGetCraftFromIndex((int)id, out Craft oraft))
		{
			GD.PrintErr("Craft not found");
			return;
		}
		if (IsMissionDestination(_destinationCellIndex) &&
		    !oraft.HasDeployableUnits)
		{
			SetupTree();
			return;
		}

		if (_destinationCellIndex >= 0)
		{
			int destinationCellIndex = _destinationCellIndex;
			_destinationCellIndex = -1;
			_ = teamBase.SendCraft(
				oraft.CurrentCellIndex,
				destinationCellIndex,
				oraft,
				teamManager);
		}
		else
		{
			GD.Print("Setting Send Craft Mode to true");
			teamManager.SetSendCraftMode(true, teamHolder, oraft);
		}

		_ = HideCall();
	}

	private static bool IsMissionDestination(int cellIndex)
	{
		if (cellIndex < 0) return false;
		GlobeMissionManager missionManager = GlobeMissionManager.Instance;
		return missionManager != null &&
		       missionManager.GetActiveMissions().TryGetValue(
			       cellIndex,
			       out MissionCellDefinition mission) &&
		       mission != null &&
		       !mission.missionStatus.HasFlag(Enums.MissionStatus.Visited);
	}

	protected override void _Hide()
	{
		_destinationCellIndex = -1;
		base._Hide();
	}
}
