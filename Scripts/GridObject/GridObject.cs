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
	public GridObject parent { get => this; set { } }

	public GridPositionData GridPositionData { get; protected set; }
	[Export] public Enums.UnitTeam Team { get; private set; }

	[Export] protected Node GridObjectNodeHolder;
	[Export] public CollisionObject3D collisionShape;
	[Export] public Node3D objectCenter;
	public GridObjectTeamHolder TeamHolder { get; protected set; }
	[Export] public Array<GridObjectNode> gridObjectNodes = new Array<GridObjectNode>();

	[Export] public Enums.GridObjectSettings gridObjectSettings = Enums.GridObjectSettings.None;

	public bool IsInitialized { get; protected set; } = false;
	[Export] public bool IsActive { get; protected set; } = true;

	public override void _EnterTree()
	{
		AddToGroup("GridObjects");
		base._EnterTree();
	}

	public virtual async Task Initialize(Enums.UnitTeam team, GridCell gridCell)
	{
		TeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(team);
		Team = team;

		GridPositionData ??= GetNodeOrNull<GridPositionData>("GridPositionData") ??
		                     GridObjectNodeHolder?.GetNodeOrNull<GridPositionData>("GridPositionData");
		
		
		GridPositionData = GridObjectNodeHolder == null ? GetNodeOrNull<GridPositionData>("GridPositionData") : GridObjectNodeHolder.GetNodeOrNull<GridPositionData>("GridPositionData");
		if (GridPositionData == null)
		{
			// If not found, create it
			GridPositionData = new GridPositionData();
			GridPositionData.Name = "GridPositionData";
			AddChild(GridPositionData);
		}

		if (gridCell == null)
		{
			GridSystem.Instance.TryGetGridCellFromWorldPosition(GridPositionData.Position, out gridCell, true);
		}

		InitializeGridObjectNodes();
		GridPositionData.SetupCall(this);

		// 1. SET DIRECTION FIRST based on World Rotation
		// This ensures that when we call SetGridCell, it calculates the offsets using the correct rotation.
		var dir = GridPositionData.GetNearestDirectionFromRotation(GlobalRotation.Y);
		GridPositionData.SetDirection(dir);

		// 2. FIND CELL
		if (gridCell == null)
		{
			// Try to snap to the grid based on current position
			GridSystem.Instance.TryGetGridCellFromWorldPosition(GridPositionData.GlobalPosition, out gridCell, true);
		}

		// 3. PLACE ON GRID
		// Now that Direction is set, this will occupy the correct rotated shape cells
		GridPositionData.SetGridCell(gridCell);

		await Task.Yield();
    
		IsInitialized = true;
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
					out List<GridObjectNode> gridObjectNodesList))
			{
				GD.Print("Error: GridObjectNodeHolder doesn't contain any GridObjectNode");
			}
			else
			{
				foreach (var node in gridObjectNodesList)
				{
					if (node == null) continue;
					gridObjectNodesArray.Add(node);
					node.SetupCall(this);
				}
			}
		}

		this.gridObjectNodes = gridObjectNodesArray;
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
		// If becoming inactive, remove from grid logic
		if (!isActive && GridPositionData != null)
		{
			GridPositionData.SetGridCell(null);
		}
	}

	public bool TryGetGridObjectNode<T>(out T node) where T : GridObjectNode
	{
		node = null;
		if (gridObjectNodes == null) return false;

		node = gridObjectNodes.FirstOrDefault(n => n is T) as T;
		return node != null;
	}

	#region Saving and Loading

	public virtual Godot.Collections.Dictionary<string, Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>();

		// 1. Identity
		// Important: SceneFilePath is required for the TeamHolder to instantiate this object
		data["Filename"] = SceneFilePath; 
		data["Name"] = Name;
		data["Team"] = (int)Team;
		data["IsActive"] = IsActive;
		data["Settings"] = (int)gridObjectSettings;

		// 2. Position
		// We save the Grid Coordinates of the root cell.
		if (GridPositionData.AnchorCell != null)
		{
			data["HasPosition"] = true;
			data["GridX"] = GridPositionData.AnchorCell.gridCoordinates.X;
			data["GridY"] = GridPositionData.AnchorCell.gridCoordinates.Y;
			data["GridZ"] = GridPositionData.AnchorCell.gridCoordinates.Z;
			data["Direction"] = (int)GridPositionData.Direction;
		}
		else
		{
			data["HasPosition"] = false;
		}

		// 3. Components (GridObjectNodes)
		// Saves the state of children components (e.g., Stats, Inventory)
		var nodeDataDict = new Godot.Collections.Dictionary<string, Variant>();
		
		// Ensure nodes are gathered
		if(gridObjectNodes == null || gridObjectNodes.Count == 0) InitializeGridObjectNodes();

		foreach (var node in gridObjectNodes)
		{
			nodeDataDict[node.Name] = node.Save();
		}
		data["Nodes"] = nodeDataDict;

		return data;
	}

	public virtual async void Load(Godot.Collections.Dictionary<string, Variant> data)
{
    // 1. Basic Data
    if (data.ContainsKey("Name")) Name = data["Name"].AsString();
    if (data.ContainsKey("Settings")) gridObjectSettings = (Enums.GridObjectSettings)(int)data["Settings"];
    
    Enums.UnitTeam team = Enums.UnitTeam.None;
    if (data.ContainsKey("Team")) team = (Enums.UnitTeam)(int)data["Team"];
    
    bool isActive = true;
    if (data.ContainsKey("IsActive")) isActive = data["IsActive"].AsBool();

    // 2. Position and Initialization
    GridCell cell = null;
    Enums.Direction savedDirection = Enums.Direction.North; // Default direction
    bool hasPosition = false;

    if (data.ContainsKey("HasPosition") && data["HasPosition"].AsBool())
    {
        hasPosition = true;
        int x = (int)data["GridX"];
        int y = (int)data["GridY"];
        int z = (int)data["GridZ"];
        
        // We need to fetch the actual GridCell from the system
        cell = GridSystem.Instance.GetGridCell(new Vector3I(x, y, z));
        
        if (data.ContainsKey("Direction"))
        {
            savedDirection = (Enums.Direction)(int)data["Direction"];
        }
    }

    // Run standard initialization to wire up the TeamHolder and set up the basic structure
    // This will place the object physically on the map
    await Initialize(team, hasPosition ? null : cell); // Pass null if we have position data to load
    
    // Set the saved direction BEFORE setting the grid cell
    if (hasPosition)
    {
        GridPositionData.SetDirection(savedDirection);
    }
    
    SetIsActive(isActive);

    // 3. Components (GridObjectNodes) - Load node data AFTER initialization
    if (data.ContainsKey("Nodes"))
    {
        var nodeDataDict = (Godot.Collections.Dictionary<string, Variant>)data["Nodes"];
        
        foreach (var node in gridObjectNodes)
        {
            if (nodeDataDict.ContainsKey(node.Name))
            {
                var nodeData = (Godot.Collections.Dictionary<string, Variant>)nodeDataDict[node.Name];
                node.Load(nodeData);
            }
        }
    }

    // 4. NOW set the grid position AFTER nodes are loaded (important for inventory, stats, etc.)
    if (hasPosition && cell != null)
    {
        // Force update the position to ensure it's correctly placed
        GridPositionData.SetGridCell(cell);
        GlobalPosition = cell.worldCenter;
    }
}

	#endregion
}