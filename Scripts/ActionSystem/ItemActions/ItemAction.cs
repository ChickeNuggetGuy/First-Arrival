using System.Collections.Generic;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.ActionSystem.ItemActions;

public abstract partial class ItemAction : Action
{
	protected ItemAction()
	{
		
	}
	protected ItemAction(GridObject parentGridObject, GridCell startingGridCell, GridCell targetGridCell, (Dictionary<Enums.Stat, int> costs, Dictionary<string, Variant> extraData) data) : base(parentGridObject, startingGridCell, targetGridCell, data)
	{
		
	}
}