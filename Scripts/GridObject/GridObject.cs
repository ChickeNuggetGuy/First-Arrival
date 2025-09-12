
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class GridObject : Node3D, IContextUser<GridObject>
{
	public GridObject parent { get => this; set{ } }
	
	[Export]public GridPositionData GridPositionData;
	public Enums.UnitTeam Team {get; private set;}

	[Export] protected Node GridObjectNodeHolder;

	public System.Collections.Generic.Dictionary<string, List<GridObjectNode>> gridObjectNodesDictionary = new System.Collections.Generic.Dictionary<string, List<GridObjectNode>>();
	
	[Export(PropertyHint.ResourceType, "ActionDefinition")]
	public ActionDefinition[] ActionDefinitions { get; protected set; }
	
	[Export,ExportGroup("Stats")] protected Node statHolder;
	public List<GridObjectStat> Stats
	{
		get
		{
			if (gridObjectNodesDictionary == null) return null;
			if (gridObjectNodesDictionary.Count == 0) return null;
			return	gridObjectNodesDictionary["stats"].Cast<GridObjectStat>().ToList();
		}
		private set{}
	}
	
	[Export, ExportGroup("Inventory")]
	protected Godot.Collections.Array<Enums.InventoryType> inventoryTypes = new Godot.Collections.Array<Enums.InventoryType>();

	private System.Collections.Generic.Dictionary<Enums.InventoryType, InventoryGrid> inventoryGrids =
		new System.Collections.Generic.Dictionary<Enums.InventoryType, InventoryGrid>();

	
	

	public virtual async Task Initialize(Enums.UnitTeam team, GridCell gridCell)
	{
		GridPositionData.SetGridCell(gridCell);
		Team = team;
		
		InitializeGridObjectNodes();
		await Task.Yield();
		InitializeRuntimeInventories();
		InitializeActionDefinitions();
		GridPositionData.SetDirection(RotationHelperFunctions.GetDirectionFromVector3(-Transform.Basis.Z));
	}

	private void InitializeActionDefinitions()
	{
		if (ActionDefinitions == null) return;

		foreach (var actionDefinition in ActionDefinitions)
		{
			if (actionDefinition == null)
			{
				GD.Print("actionDefinition is null");
				continue;
			}
			actionDefinition.parentGridObject = this;
		}
	}
	private void InitializeGridObjectNodes()
	{
		if (GridObjectNodeHolder == null) return;

		System.Collections.Generic.Dictionary<string, List<GridObjectNode>> gridObjectNodesDict =
			new System.Collections.Generic.Dictionary<string, List<GridObjectNode>>()
			{
				{ "all", new List<GridObjectNode>() },
				{ "stats", new List<GridObjectNode>() },
				{ "actionDefinitions", new List<GridObjectNode>() }
			};

		if (!this.TryGetAllComponentsInChildrenRecursive<GridObjectNode>(out List<GridObjectNode> gridObjectNodes))
			return;

		foreach (var node in gridObjectNodes)
		{
			gridObjectNodesDict["all"].Add(node);
			node.SetupCall(this);
			if (node == null) continue;
			else if (node is GridObjectStat stat)
			{
				gridObjectNodesDict["stats"].Add(stat);
			}
			else
			{
				GD.Print("GridObjectNode found was not properly sorted!");
			}
		}

		gridObjectNodesDictionary =  gridObjectNodesDict;
	}

	private void InitializeRuntimeInventories()
	{
		InventoryManager inventoryManager = InventoryManager.Instance;
		if (inventoryManager == null) return;
		foreach (Enums.InventoryType inventoryType in inventoryTypes)
		{
			InventoryGrid inventory = inventoryManager.GetInventoryGrid(inventoryType);
			if (inventory == null) continue;
			inventoryGrids.Add(inventoryType, inventory);

			if (inventoryType == Enums.InventoryType.Backpack)
			{
				inventory.TryAddItem(InventoryManager.Instance.GetRandomItem());
			}
		}	
	}
	
	public bool TryGetStat(Enums.Stat statToFind, out GridObjectStat stat)
	{
		stat = null;
		if (gridObjectNodesDictionary["stats"] == null) return false;

		stat = (GridObjectStat)gridObjectNodesDictionary["stats"].FirstOrDefault(gridObjectNode =>
		    {
				if(gridObjectNode is  not GridObjectStat statObj)return false;
				if(statObj.Stat == statToFind) return true;
				return false;
		    });
		
		if (stat == null) return false;
		else return true;
	}
	
	public bool CanAffordStatCost(System.Collections.Generic.Dictionary<Enums.Stat, int> costs)
	{
		foreach (var stat in costs)
		{
			GridObjectStat statObj = Stats.FirstOrDefault(statObj => statObj.Stat == stat.Key);
			
			if (statObj == null) return false;
			
			if (stat.Value  > statObj.CurrentValue ) return false;
		}
		return true;
	}

	public bool TryGetInventory(Enums.InventoryType inventoryType, out InventoryGrid inventory)
	{
		inventory = null;
		if (inventoryGrids == null) return false;
		if (!inventoryGrids.ContainsKey(inventoryType)) return false;
		
		inventory = inventoryGrids[inventoryType];
		return true;
		
	}


	public Dictionary<String,Callable> GetContextActions()
	{
		Dictionary<String,Callable> actions = new();
		
		foreach (var action in ActionDefinitions)
		{
			actions.Add(action.GetActionName() ,Callable.From(() => ActionManager.Instance.SetSelectedAction(action)));
		}
		foreach (var inventoryPair in inventoryGrids)
		{
			if (inventoryPair.Value == null) continue;
			if(!inventoryPair.Value.InventorySettings.HasFlag(Enums.InventorySettings.IsEquipmentinventory)) continue;
			
			if(inventoryPair.Value.ItemCount < 1) continue;
			
			List<Item> items = inventoryPair.Value.uniqueItems;

			foreach (var item in items)
			{
				Dictionary<String, Callable> itemCallables = new Dictionary<string, Callable>();
				foreach (var c in itemCallables)
					actions.Add(c.Key, c.Value);
			}
		}
		return actions;
	}
}