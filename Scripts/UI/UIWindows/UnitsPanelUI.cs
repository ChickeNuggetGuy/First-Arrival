using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

public partial class UnitsPanelUI : UIWindow
{
	[Export] private Texture2D unitIcon;
	[Export] private ItemList unitItemList;
	
	[Export] private UnitStatsUI unitStatsUI;
	
	[Export] private Button renameButton;
	[Export] private Button hireButton;
	[Export] private Button fireButton;
	[Export] private PackedScene unitScene;
	
	private TeamBaseCellDefinition CurrentBase
	{
		get
		{
			return GameManager.Instance.currentBase;
		}
	}


	protected override Task _Setup()
	{
		if (hireButton != null )
		{
			hireButton.Pressed += HireButtonOnPressed;
			hireButton.Text = $"Hire (${GameManager.UnitHiringCost:N0})";
		}
		
		if (fireButton != null)
		{
			fireButton.Pressed += FireButtonOnPressed;
		}
		return base._Setup();
	}

	public override void _ExitTree()
	{
		if (hireButton != null )
		{
			hireButton.Pressed -= HireButtonOnPressed;
		}
		
		if (fireButton != null)
		{
			fireButton.Pressed -= FireButtonOnPressed;
		}
		base._ExitTree();
	}

	private void FireButtonOnPressed()
	{
	}

	private void HireButtonOnPressed()
	{
		if (!GameManager.Instance.TryHireUnits()) return;

		ContstructUnitList();
		GD.Print($"Hired a unit for ${GameManager.UnitHiringCost:N0}. Total units at base: {CurrentBase.GetStationedGridObjects().Count}");
	}

	
	protected override async Task DrawUI()
	{
		base._Show();
		ContstructUnitList();
	}

	private void ContstructUnitList()
	{
		if (hireButton != null)
			hireButton.Disabled = !GameManager.Instance.CanHireUnits();

		if(unitItemList == null)
			return;
		if (CurrentBase == null)
			return;
		
		unitItemList.Clear();

		Array<GridObject> units = CurrentBase.GetStationedGridObjects();
		if (units.Count == 0) return;

		foreach (GridObject gridObject in units)
		{
			CreateUnitListItem(gridObject);
		}
	}

	private void CreateUnitListItem(GridObject gridObject)
	{
		if (gridObject == null)return;
		
		unitItemList.AddItem(gridObject.Name, unitIcon );
	}
	
}
