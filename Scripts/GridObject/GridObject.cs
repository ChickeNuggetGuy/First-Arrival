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
public partial class GridObject : StaticBody3D, IContextUser<GridObject>
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
	[Export] private Array<GridObjectNode> gridObjectNodes = new();

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

	public virtual async Task Initialize(
		Enums.UnitTeam team,
		GridCell gridCell,
		bool allowMissingGridCell = false
	)
	{
		// Stored globe/base units are loaded as inactive data objects in scenes
		// that intentionally do not contain the battle-only GridObjectManager.
		// They receive a TeamHolder later when initialized in a battle scene.
		TeamHolder = GridObjectManager.Instance?.GetGridObjectTeamHolder(team);
		Team = team;

		GridPositionData ??= GetNodeOrNull<GridPositionData>("GridPositionData") ??
		                     GridObjectNodeHolder?.GetNodeOrNull<GridPositionData>(
			                     "GridPositionData"
		                     );

		if (GridObjectNodeHolder == null)
		{
			GridObjectNodeHolder = new Node3D();
			GridObjectNodeHolder.Name = "GridObjectNodeHolder";
			AddChild(GridObjectNodeHolder);
		}

		if (GridPositionData == null)
		{
			GridPositionData = new GridPositionData();
			GridPositionData.Name = "GridPositionData";
			GridObjectNodeHolder.AddChild(GridPositionData);
		}

		if (gridCell == null && !allowMissingGridCell)
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
		{
			GridPositionData.CalculateShapeFromColliders();
		}

		if (gridCell != null)
		{
			GridPositionData.SetGridCell(gridCell);
		}
		else if (!allowMissingGridCell)
		{
			GD.PrintErr(
				$"GridObject {Name}: Initialize called but GridCell is null " +
				$"(could not find cell at {GridPositionData.GlobalPosition})"
			);
		}

		await Task.Yield();
		IsInitialized = true;
	}


	private void InitializeGridObjectNodes()
	{
		if (GridObjectNodeHolder == null)
		{
			GridObjectNodeHolder = new Node3D();
			GridObjectNodeHolder.Name = "GridObjectNodeHolder";
			AddChild(GridObjectNodeHolder);
		}

		if (gridObjectNodes == null || gridObjectNodes.Count == 0)
		{
			if (!this.TryGetAllComponentsInChildrenRecursive(out List<GridObjectNode> gridObjectNodesList))
			{
				GD.PrintErr("Error: GridObjectNodeHolder doesn't contain any GridObjectNode");
			}
			else
			{
				gridObjectNodes = new Array<GridObjectNode>();
				foreach (var node in gridObjectNodesList)
				{
					if (node == null) continue;
					gridObjectNodes.Add(node);
				}
			}
		}

		foreach (var node in gridObjectNodes)
		{
			if (node == null) continue;
			node.SetupCall(this);
		}
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

		if (GridPositionData?.AnchorCell != null)
		{
			data["HasPosition"] = true;
			data["GridX"] = GridPositionData.AnchorCell.GridCoordinates.X;
			data["GridY"] = GridPositionData.AnchorCell.GridCoordinates.Y;
			data["GridZ"] = GridPositionData.AnchorCell.GridCoordinates.Z;
			data["Direction"] = (int)GridPositionData.Direction;
		}
		else
		{
			data["HasPosition"] = false;
		}

		var nodeDataDict =
			new Godot.Collections.Dictionary<string, Variant>();

		if (gridObjectNodes == null || gridObjectNodes.Count == 0)
		{
			InitializeGridObjectNodes();
		}

		foreach (var node in gridObjectNodes)
		{
			if (node == null) continue;
			nodeDataDict[node.Name] = node.Save();
		}

		data["Nodes"] = nodeDataDict;

		return data;
	}

	public virtual async void Load(
		Godot.Collections.Dictionary<string, Variant> data
	)
	{
		await LoadAsync(data);
	}

	public virtual async Task LoadAsync(
		Godot.Collections.Dictionary<string, Variant> data
	)
	{
		if (data.ContainsKey("Name")) Name = data["Name"].AsString();
		if (data.ContainsKey("Settings"))
		{
			gridObjectSettings =
				(Enums.GridObjectSettings)(int)data["Settings"];
		}

		Enums.UnitTeam team = Enums.UnitTeam.None;
		if (data.ContainsKey("Team"))
		{
			team = (Enums.UnitTeam)(int)data["Team"];
		}

		bool isActive = true;
		if (data.ContainsKey("IsActive"))
		{
			isActive = data["IsActive"].AsBool();
		}

		GridCell cell = null;
		Enums.Direction savedDirection = Enums.Direction.North;
		bool hasPosition = false;

		if (data.ContainsKey("HasPosition") && data["HasPosition"].AsBool())
		{
			hasPosition = true;

			int x = data["GridX"].AsInt32();
			int y = data["GridY"].AsInt32();
			int z = data["GridZ"].AsInt32();

			cell = GridSystem.Instance.GetGridCell(new Vector3I(x, y, z));

			if (data.ContainsKey("Direction"))
			{
				savedDirection =
					(Enums.Direction)data["Direction"].AsInt32();
			}
		}

		await Initialize(team, hasPosition ? cell : null, !hasPosition);

		if (hasPosition)
		{
			GridPositionData.SetDirection(savedDirection);
		}

		SetIsActive(isActive);

		if (data.ContainsKey("Nodes"))
		{
			var nodeDataDict =
				data["Nodes"].AsGodotDictionary();

			foreach (var node in gridObjectNodes)
			{
				if (node == null) continue;
				if (!nodeDataDict.ContainsKey(node.Name)) continue;

				var nodeData =
					(Godot.Collections.Dictionary<string, Variant>)
					nodeDataDict[node.Name];

				node.Load(nodeData);
			}
		}

		if (hasPosition && cell != null)
		{
			GridPositionData.SetGridCell(cell);
		}
	}



	#endregion
}
