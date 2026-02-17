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
	public GridObject parent
	{
		get => this;
		set { }
	}

	public GridPositionData GridPositionData { get; protected set; }
	[Export] public Enums.UnitTeam Team { get; private set; }

	[Export] protected Node GridObjectNodeHolder;
	[Export] public CollisionObject3D collisionShape;
	[Export] public Node3D objectCenter;
	[Export] public Node3D visualMesh;
	[Export] public Enums.Stance CurrentStance = Enums.Stance.Normal;
	[Export] public BoneAttachment3D LeftHandBoneAttachment;
	[Export] public BoneAttachment3D RightHandBoneAttachment;
	public GridObjectTeamHolder TeamHolder { get; protected set; }
	[Export] public Array<GridObjectNode> gridObjectNodes = new Array<GridObjectNode>();

	[Export] public Enums.GridObjectSettings gridObjectSettings = Enums.GridObjectSettings.None;
	
	[Export] public GridObjectAnimation animationNode;
	[Export] public bool scenery = false;

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
		                     GridObjectNodeHolder?.GetNodeOrNull<GridPositionData>(
			                     "GridPositionData"
		                     );

		if (GridObjectNodeHolder == null)
		{
			GridObjectNodeHolder = new Node3D();
			AddChild(GridObjectNodeHolder);
		}

		if (GridPositionData == null)
		{
			GridPositionData = new GridPositionData();
			GridPositionData.Name = "GridPositionData";
			GridObjectNodeHolder.AddChild(GridPositionData);
		}

		if (gridCell == null)
		{
			GridSystem.Instance.TryGetGridCellFromWorldPosition(
				GridPositionData.GlobalPosition,
				out gridCell,
				true
			);
		}

		InitializeGridObjectNodes();
		GridPositionData.SetupCall(this);

		if (GridPositionData.AutoCalculateShape)
			GridPositionData.CalculateShapeFromColliders();
		
		if (gridCell != null)
			GridPositionData.SetGridCell(gridCell);
		else
			GD.PrintErr($"GridObject {Name}: Initialize called but GridCell is null (could not find cell at {GridPositionData.GlobalPosition})");

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
			if (!GridObjectNodeHolder.TryGetAllComponentsInChildrenRecursive<GridObjectNode>(
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
	
		if (!isActive && GridPositionData != null)
		{
			// remove from grid logic
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

		data["Filename"] = SceneFilePath;
		data["Name"] = Name;
		data["Team"] = (int)Team;
		data["IsActive"] = IsActive;
		data["Settings"] = (int)gridObjectSettings;

		// Position
		if (GridPositionData.AnchorCell != null)
		{
			data["HasPosition"] = true;
			data["GridX"] = GridPositionData.AnchorCell.GridCoordinates.X;
			data["GridY"] = GridPositionData.AnchorCell.GridCoordinates.Y;
			data["GridZ"] = GridPositionData.AnchorCell.GridCoordinates.Z;
			data["Direction"] = (int)GridPositionData.Direction;
		}
		else
		{
			GD.Print("Has position false");
			data["HasPosition"] = false;
		}

		// Saves the state of children components
		var nodeDataDict = new Godot.Collections.Dictionary<string, Variant>();

		// Ensure nodes are gathered
		if (gridObjectNodes == null || gridObjectNodes.Count == 0) InitializeGridObjectNodes();

		foreach (var node in gridObjectNodes)
		{
			nodeDataDict[node.Name] = node.Save();
		}

		data["Nodes"] = nodeDataDict;

		return data;
	}

	public virtual async void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		// Basic Data
		if (data.ContainsKey("Name")) Name = data["Name"].AsString();
		if (data.ContainsKey("Settings")) gridObjectSettings = (Enums.GridObjectSettings)(int)data["Settings"];

		Enums.UnitTeam team = Enums.UnitTeam.None;
		if (data.ContainsKey("Team")) team = (Enums.UnitTeam)(int)data["Team"];

		bool isActive = true;
		if (data.ContainsKey("IsActive")) isActive = data["IsActive"].AsBool();

		GridCell cell = null;
		Enums.Direction savedDirection = Enums.Direction.North;
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

		if (hasPosition)
		{
			GD.Print($"Team {team} has position {cell}");
			await Initialize(team, cell);
		}
		else
		{
			await Initialize(team, null);
		}

		if (hasPosition)
		{
			GridPositionData.SetDirection(savedDirection);
		}

		SetIsActive(isActive);

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

		if (hasPosition && cell != null)
		{
			// Force update the position 
			GridPositionData.SetGridCell(cell);
		}
	}

	#endregion
}