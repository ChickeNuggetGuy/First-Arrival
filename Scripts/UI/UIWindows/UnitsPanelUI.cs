using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
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
		if (hireButton != null)
		{
			hireButton.Pressed += HireButtonOnPressed;
		}
		
		if (fireButton != null)
		{
			fireButton.Pressed += FireButtonOnPressed;
		}
		return base._Setup();
	}

	private void FireButtonOnPressed()
	{
		throw new NotImplementedException();
	}

	private void HireButtonOnPressed()
	{
		GridObject newUnit = unitScene.Instantiate<GridObject>();
		GD.Print(newUnit.Name);
		CurrentBase.TryAddStationedGridObject(newUnit);
		ContstructUnitList();
	}

	protected override void _Show()
	{
		base._Show();
		ContstructUnitList();
	}

	private void ContstructUnitList()
	{
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
