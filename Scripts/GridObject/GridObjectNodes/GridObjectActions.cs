using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
	
	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var retVal = new Godot.Collections.Dictionary<string, Variant>();
        
		Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> actionArray = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
		foreach (var action in ActionDefinitions)
		{
			var actionData = new Godot.Collections.Dictionary<string, Variant>();
			actionData.Add("resource_path", action.ResourcePath);
			actionData.Add("action_name", action.GetActionName());
			// Add any other serializable properties of the action definition here
			actionArray.Add(actionData);
		}
		retVal.Add("actions", actionArray);
		return retVal;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!data.ContainsKey("actions")) return;
        
		var actionArray = (Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>)data["actions"];
		var loadedActions = new Godot.Collections.Array<ActionDefinition>();

		foreach (var actionData in actionArray)
		{
			if (actionData.ContainsKey("resource_path"))
			{
				string resourcePath = actionData["resource_path"].AsString();
				var actionDefinition = GD.Load<ActionDefinition>(resourcePath);
                
				if (actionDefinition != null)
				{
					// Setup the parent reference
					actionDefinition.parentGridObject = parentGridObject;
					loadedActions.Add(actionDefinition);
				}
				else
				{
					GD.PrintErr($"Failed to load action definition from path: {resourcePath}");
				}
			}
		}

		ActionDefinitions = loadedActions.ToArray();
	}
}
