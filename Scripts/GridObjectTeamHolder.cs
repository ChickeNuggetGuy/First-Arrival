using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GridObjectTeamHolder : Node
{
	[Export] public Enums.UnitTeam team { get; private set; }
	public Dictionary<Enums.GridObjectState, Array<GridObject>> GridObjects { get; protected set; }  =  new Dictionary<Enums.GridObjectState, Array<GridObject>>()
	{
		{ Enums.GridObjectState.Active , new Array<GridObject>()},
		{ Enums.GridObjectState.Inactive , new Array<GridObject>()},
	};
	public GridObject CurrentGridObject { get; protected set; }
	
	[Signal]
	public delegate void SelectedGridObjectChangedEventHandler(GridObject gridObject);
	public GridObject GetNextGridObject()
	{
		int index = GridObjects[Enums.GridObjectState.Active].IndexOf(CurrentGridObject);
		
		if(index == -1)
			return null;
		
		int nextIndex = ((index + 1 )>= GridObjects[Enums.GridObjectState.Active].Count ) ? 0 : index + 1;

		SetSelectedGridObject(GridObjects[Enums.GridObjectState.Active][nextIndex]);
		return CurrentGridObject;
	}

	public void SetSelectedGridObject(GridObject gridObject)
	{
		GridObject oldGridObject = CurrentGridObject;
		CurrentGridObject = gridObject;
		GD.Print("1");
		EmitSignal(SignalName.SelectedGridObjectChanged, CurrentGridObject);
		
	}

	public void AddGridObject(GridObject gridObject)
	{
		GridObjects[Enums.GridObjectState.Active].Add(gridObject);
	}
}
