using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public abstract partial class ActionDefinition : Resource	
{
	public GridObject parentGridObject {get; set;}
	
	public List<GridCell> ValidGridCells { get; protected set; } = new List<GridCell>();
	[Export]public bool remainSelected {get; private set;} = false;

	public async Task InstantiateActionCall(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		Dictionary<Enums.Stat, int> costs, bool executeAfterCreation = true)
	{
		Action action = InstantiateAction(parent, startGridCell, targetGridCell, costs);
		if (executeAfterCreation)
		{
			await action.ExecuteCall();
		}

	}
	
	
	public abstract Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell, Dictionary<Enums.Stat, int> costs);

	public abstract bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell,
		out Dictionary<Enums.Stat, int> costs,out string reason);


	public void UpdateValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		ValidGridCells.Clear();
		ValidGridCells.AddRange(GetValidGridCells(gridObject, startingGridCell));
	}
	protected abstract List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell);

	public abstract bool GetIsUIAction();
	
	public abstract string GetActionName();
	
	public abstract MouseButton GetActionInput();
	public abstract bool GetIsAlwaysActive();
}
