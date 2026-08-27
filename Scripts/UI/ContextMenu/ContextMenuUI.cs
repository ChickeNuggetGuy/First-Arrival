using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class ContextMenuUI : UIWindow
{
	[Export] private Control contextButtonHolder;
	List<ContextMenuButtonUI> contextButtons = new List<ContextMenuButtonUI>();

	[Export] private PackedScene contextButtonScene;


	protected override Task _Setup()
	{
		return Task.CompletedTask;
	}

	
	protected override Task DrawUI()
	{
		GD.Print("ContextMenuUI Show");
		Position = GetViewport().GetMousePosition();
		base._Show();
		return Task.CompletedTask;
	}

	private bool TryGenerateContextMenu(IContextUserBase contextUser)
	{
		return contextUser != null &&
		       TryGenerateContextMenu(contextUser.GetContextActions());
	}

	private bool TryGenerateContextMenu(Dictionary<String, Callable> callables)
	{
		ClearContextButtons();
		if (callables == null || callables.Count == 0)
		{
			GD.Print("Callables was null or 0!");
			return false;
		}

		foreach (var c in callables)
		{
			CreateContextButton(c.Key, c.Value);
		}

		return true;
	}

	private bool TryGenerateGlobeContextMenu(HexCellData cell)
	{
		var actions = new Dictionary<string, Callable>();
		AddActions(actions, new HexCellDefinition(cell.Index, $"Hex {cell.Index}"));

		foreach (Node node in GetTree().GetNodesInGroup(
			         CellDefinitionVisual.ContextUserGroup))
		{
			if (node is not CellDefinitionVisual visual ||
			    visual.CellIndex != cell.Index)
			{
				continue;
			}

			HexCellDefinition definition = visual.parentCellDefinition;
			if (definition == null ||
			    !definition.IsVisibleTo(Enums.UnitTeam.Player)) continue;

			AddActions(actions, definition);
		}

		return TryGenerateContextMenu(actions);
	}

	private static void AddActions(
		Dictionary<string, Callable> destination,
		IContextUserBase contextUser)
	{
		if (contextUser == null) return;
		Dictionary<string, Callable> source = contextUser.GetContextActions();
		if (source == null) return;

		foreach (var action in source)
		{
			// Multiple definitions can occupy a hex. Shared actions such as Focus
			// should appear only once while type-specific actions are combined.
			if (!destination.ContainsKey(action.Key))
				destination.Add(action.Key, action.Value);
		}
	}

	private void ClearContextButtons()
	{
		foreach (ContextMenuButtonUI contextButton in contextButtons)
		{
			contextButton.QueueFree();
		}

		contextButtons.Clear();
	}

	private void CreateContextButton(String name, Callable callable)
	{
		ContextMenuButtonUI contextButton = contextButtonScene.Instantiate() as ContextMenuButtonUI;
		if (contextButton == null) return;

		contextButton.Init(this, callable, name);
		contextButtons.Add(contextButton);
		contextButtonHolder.AddChild(contextButton);
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Right &&
		    mouseEvent.Pressed)
		{
			if (GameManager.Instance.currentScene == GameManager.GameScene.BattleScene)
			{
				GD.Print("Context Menu: Right Clicked");

				IContextUserBase hoveredContextUser = GetHoveredContextUser();
				if (hoveredContextUser != null)
				{
					if (TryGenerateContextMenu(hoveredContextUser))
					{
						GD.Print("Context Menu: Context UI Item");
							_ = ShowCall();
					}
					return;
				}

				// No context-aware UI is under the cursor, so check the 3D world.
				GodotObject obj = BattleInputManager.Instance.GetObjectAtMousePosition(out Vector3 hitPosition);

				if (obj != null)
				{
					if (obj is IContextUserBase contextUser)
					{
						if (obj is GridObject gridObject)
						{
							if (gridObject.Team != Enums.UnitTeam.Player) return;
						}

						if (!TryGenerateContextMenu(contextUser)) return;
						GD.Print("Context Menu: Context Item");
							_ = ShowCall();
					}
				}
			}
			else if (GameManager.Instance.currentScene == GameManager.GameScene.GlobeScene)
			{
				HexCellData? hexCellData = GlobeInputManager.Instance?.CurrentCell;
				if (hexCellData.HasValue &&
				    TryGenerateGlobeContextMenu(hexCellData.Value))
				{
						_ = ShowCall();
				}
				else
				{
						_ = HideCall();
				}
			}
		}
		else if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			if (!IsShown) return;

			var hoveredControl = GetViewport()?.GuiGetHoveredControl();

			// Check if the hovered control is part of the context menu hierarchy
			if (hoveredControl != null && !IsPartOfContextMenu(hoveredControl))
			{
					_ = HideCall();
			}
		}
	}

	private IContextUserBase GetHoveredContextUser()
	{
		// GuiGetHoveredControl can return a child Label or TextureRect rather
		// than the ItemSlotUI itself, so walk up to the first context user.
		Node hoveredNode = GetViewport()?.GuiGetHoveredControl();
		while (hoveredNode != null)
		{
			if (hoveredNode is IContextUserBase contextUser)
				return contextUser;

			hoveredNode = hoveredNode.GetParent();
		}

		return null;
	}

	private bool IsPartOfContextMenu(Control control)
	{
		// Traverse up the hierarchy to see if the control is part of the context menu
		while (control != null)
		{
			if (control == this)
			{
				return true;
			}

			control = control.GetParent() as Control;
		}

		return false;
	}
}
