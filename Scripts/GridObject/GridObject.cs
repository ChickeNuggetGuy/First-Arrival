
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GridObject : Node3D, IContextUser<GridObject>
{
	public GridObject parent { get => this; set{ } }
	
	[Export]public GridPositionData GridPositionData;
	[Export]public Enums.UnitTeam Team {get; private set;}

	[Export] protected Node GridObjectNodeHolder;
	[Export] public CollisionObject3D collisionShape;
	[Export] public Node3D objectCenter;
	public GridObjectTeamHolder TeamHolder { get; protected set; }
	[Export]public Array<GridObjectNode> gridObjectNodes = new Array<GridObjectNode>();

	
	[Export]public Enums.GridObjectSettings gridObjectSettings = Enums.GridObjectSettings.None;

	public bool IsInitialized { get; protected set; }= false;
	[Export] public bool IsActive { get; protected set; } = true;

	

	public virtual async Task Initialize(Enums.UnitTeam team, GridCell gridCell)
	{
		TeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(team);
		GD.Print($"GridObject: {Name} :Initializing");
		Team = team;

		if (gridCell == null)
		{
			GridSystem.Instance.TryGetGridCellFromWorldPosition(GridPositionData.GlobalPosition, out gridCell, true);
		}

		InitializeGridObjectNodes();
		GridPositionData.Setup();
		GridPositionData.SetGridCell(gridCell);

		await Task.Yield();
		GridPositionData.SetDirection(Enums.Direction.North);
		IsInitialized = true;
		GD.Print($"GridObject: {Name} :Initialized");
	}


	private void InitializeGridObjectNodes()
	{
		Array<GridObjectNode> gridObjectNodesArray = new Array<GridObjectNode>() { };
		if (GridObjectNodeHolder == null)
		{
			Node gridObjectNode = new Node();
			gridObjectNode.Name = "Grid Object Node Holder";
			AddChild(gridObjectNode);
		}
		else
		{
			if (!GridObjectNodeHolder.TryGetAllComponentsInChildren<GridObjectNode>(
				    out List<GridObjectNode> gridObjectNodes))
			{
				GD.Print("Error: GridObjectNodeHolder doesn't contain any GridObjectNode");
			}
			else
			{
				foreach (var node in gridObjectNodes)
				{
					if (node == null) continue;
					gridObjectNodesArray.Add(node);
					node.SetupCall(this);

				}
			}
		}

		this.gridObjectNodes =  gridObjectNodesArray;
	}

	public System.Collections.Generic.Dictionary<String, Callable> GetContextActions()
	{
		System.Collections.Generic.Dictionary<String, Callable> actions = new();

		foreach (var gridObjectNode in gridObjectNodes)
		{
			if (gridObjectNode == null) continue;

			if (gridObjectNode is IContextUser<GridObjectNode> contextUser)
			{
				var nodeActions = contextUser.GetContextActions();
				foreach (var nodeAction in nodeActions)
				{
					actions.Add(nodeAction.Key, nodeAction.Value);
				}
			}
		}
		
		return actions;
	}

	public void SetIsActive(bool isActive)
	{
		IsActive = isActive;
		GridPositionData.SetGridCell(null);
	}

	public bool TryGetGridObjectNode<T>(out T node) where T : GridObjectNode
	{
		node = null;
		if(gridObjectNodes == null) return false;
		
		node = gridObjectNodes.FirstOrDefault(n => n is T) as T;
		return node != null;
	}
}