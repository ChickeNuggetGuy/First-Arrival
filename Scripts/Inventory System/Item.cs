using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using Godot;

namespace FirstArrival.Scripts.Inventory_System;

public partial class Item : Node3D, IContextUser<Item>
{
	public Item parent
	{
		get => this; set{} }
	public ItemData ItemData { get; protected set; }
	[Export]public Node3D Visual { get;protected set; }
	
	public InventoryGrid currentGrid{get;set;}
	public void Init(ItemData itemData)
	{
		ItemData = itemData;
	}

	public Dictionary<string,Callable> GetContextActions()
	{
		Dictionary<string,Callable> actions = new Dictionary<string,Callable>();
		foreach (var action in ItemData.ActionDefinitions)
		{
			actions.Add(action.GetActionName(),Callable.From(() => ActionManager.Instance.SetSelectedAction(action,
				new Dictionary<string, Variant>()
				{
					{ "item", this }
				})));
		}
		return actions;
	}
	
}