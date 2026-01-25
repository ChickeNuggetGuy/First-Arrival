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

	protected override Task _Setup()
	{
		treeUI.HideRoot = true;
		AcceptButton.Pressed += AcceptButtonOnPressed;
		treeUI.ButtonClicked += TreeUIOnButtonClicked;
		return base._Setup();
		
	}

	public override void _ExitTree()
	{
		AcceptButton.Pressed -= AcceptButtonOnPressed;
		treeUI.ButtonClicked -= TreeUIOnButtonClicked;
		base._ExitTree();
	}

	protected override void _Show()
	{
		SetupTree();
		base._Show();
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
		foreach (var teamHolderBase in teamHolder.Bases)
		{
			if (teamHolderBase == null) continue;
			var treeChild = treeUI.CreateItem(root, teamHolderBase.cellIndex);
			treeChild.SetText(0, teamHolderBase.definitionName);

			foreach (var craft in teamHolderBase.CraftList)
			{
				var treeSubChild = treeUI.CreateItem(treeChild, craft.Index);
				treeSubChild.SetText(0, craft.ItemName);

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

		GD.Print("Setting Send Craft Mode to true");
		teamManager.SetSendCraftMode(true,teamHolder ,oraft);
		HideCall();
	}
}
