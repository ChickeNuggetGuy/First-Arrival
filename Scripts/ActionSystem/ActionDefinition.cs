using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public abstract partial class ActionDefinition : Resource	
{
	public GridObject parentGridObject {get; set;}

	
	public Dictionary<string, Variant> extraData = new Dictionary<string, Variant>();
	
	public List<GridCell> ValidGridCells { get; protected set; } = new List<GridCell>();


	public async Task InstantiateActionCall(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		(Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data
		, bool executeAfterCreation = true)
	{
		Action action = InstantiateAction(parent, startGridCell, targetGridCell, (data.costs, data.extraData));
		await action.ExecuteCall();
	}
	
	
	public abstract Action InstantiateAction(GridObject parent, GridCell startGridCell, GridCell targetGridCell,
		(Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data);

	public abstract bool CanTakeAction(GridObject gridObject, GridCell startingGridCell, GridCell targetGridCell, Dictionary<string, Variant> extraData,
		out (Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData, string reason) outdata);


	public void UpdateValidGridCells(GridObject gridObject, GridCell startingGridCell)
	{
		ValidGridCells.Clear();
		ValidGridCells.AddRange(GetValidGridCells(gridObject, startingGridCell));
	}
	protected abstract List<GridCell> GetValidGridCells(GridObject gridObject, GridCell startingGridCell);

	public abstract bool GetIsUIAction();
	
	public abstract string GetActionName();
}
