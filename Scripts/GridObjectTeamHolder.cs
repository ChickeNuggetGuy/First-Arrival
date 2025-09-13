using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class GridObjectTeamHolder : Node
{
	[Export] public Enums.UnitTeam team { get; private set; }
	[Export] private Node _activeUnitsHolder;
	[Export] private Node _inactiveUnitsHolder;

	public Dictionary<Enums.GridObjectState, List<GridObject>> GridObjects { get; protected set; }
	public GridObject CurrentGridObject { get; protected set; }
	
	[Signal]
	public delegate void SelectedGridObjectChangedEventHandler(GridObject gridObject);
	[Signal] public delegate void GridObjectListChangedEventHandler(GridObjectTeamHolder gridObjectTeamHolder);

	public override void _Ready()
	{
		GridObjects = new Dictionary<Enums.GridObjectState, List<GridObject>>()
		{
			{ Enums.GridObjectState.Active , new List<GridObject>()},
			{ Enums.GridObjectState.Inactive , new List<GridObject>()},
		};

		if (_activeUnitsHolder == null)
		{
			_activeUnitsHolder = new Node { Name = "ActiveUnits" };
			AddChild(_activeUnitsHolder);
			GD.Print($"GridObjectTeamHolder: Created _activeUnitsHolder: {_activeUnitsHolder.Name}");
		}
		else
		{
			GD.Print($"GridObjectTeamHolder: _activeUnitsHolder already set: {_activeUnitsHolder.Name}");
		}

		if (_inactiveUnitsHolder == null)
		{
			_inactiveUnitsHolder = new Node { Name = "InactiveUnits" };
			AddChild(_inactiveUnitsHolder);
			GD.Print($"GridObjectTeamHolder: Created _inactiveUnitsHolder: {_inactiveUnitsHolder.Name}");
		}
		else
		{
			GD.Print($"GridObjectTeamHolder: _inactiveUnitsHolder already set: {_inactiveUnitsHolder.Name}");
		}
	}

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

	public async Task AddGridObject(GridObject gridObject)
	{
		while (!gridObject.IsInitialized)
		{
			await Task.Yield();
		}
		GridObjects[Enums.GridObjectState.Active].Add(gridObject);


		gridObject.GetParent()?.RemoveChild(gridObject); 
		_activeUnitsHolder.AddChild(gridObject); // Reparent to active holder

		if (gridObject.TryGetStat(Enums.Stat.Health, out GridObjectStat health))
		{
			health.CurrentValueMin += HealthOnCurrentValueMin;
		}

		EmitSignal(SignalName.GridObjectListChanged, this);
	}
	

	private void HealthOnCurrentValueMin(int value, GridObject gridObject)
	{
		if (gridObject == CurrentGridObject)
		{
			//Select a new currentObject 
			GetNextGridObject();
			
		}
		GridObjects[Enums.GridObjectState.Active].Remove(gridObject);
		GridObjects[Enums.GridObjectState.Inactive].Add(gridObject);
		
		gridObject.GetParent()?.RemoveChild(gridObject); // Remove from current parent
		_inactiveUnitsHolder.AddChild(gridObject); // Add to inactive holder
		
		gridObject.SetIsActive(false);
		gridObject.Hide();
		gridObject.Position = new(-100, -100, -100);
		EmitSignal(SignalName.GridObjectListChanged, this);
	}

	public bool IsGridObjectActive(GridObject gridObject)
	{
		return GridObjects[Enums.GridObjectState.Active].Contains(gridObject);
	}
}
