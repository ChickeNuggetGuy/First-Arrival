using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class GridObjectActions : GridObjectNode, IContextUser<GridObjectNode>
{
	[Export(PropertyHint.ResourceType, "ActionDefinition")]
	public ActionDefinition[] ActionDefinitions { get; protected set; }


	protected override void Setup()
	{
		if (ActionDefinitions == null) return;

		foreach (var actionDefinition in ActionDefinitions)
		{
			if (actionDefinition == null)
			{
				GD.Print("actionDefinition is null");
				continue;
			}

			actionDefinition.parentGridObject = parentGridObject;
		}
	}

	public Dictionary<string, Callable> GetContextActions()
	{
		var actions = new Dictionary<string, Callable>();
		foreach (var action in ActionDefinitions)
		{
			actions.Add(action.GetActionName(), Callable.From(() => ActionManager.Instance.SetSelectedAction(action)));
		}
		return actions;
	}

	public GridObjectNode parent { get; set; }
}
