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
		return base._Setup();
	}

	protected override void _Show()
	{
		GD.Print("ContextMenuUI Show");
		Position = GetViewport().GetMousePosition();
		base._Show();
	}

	private bool TryGenerateContextMenu(IContextUserBase contextUser)
	{
		ClearContextButtons();
		Dictionary<String, Callable> callables = contextUser.GetContextActions();
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

				// UI takes precedence over the 3D raycast. Inventory controls can sit
				// over a GridObject, and that object must not prevent the slot from
				// providing its own context actions.
				IContextUserBase hoveredContextUser = GetHoveredContextUser();
				if (hoveredContextUser != null)
				{
					if (TryGenerateContextMenu(hoveredContextUser))
					{
						GD.Print("Context Menu: Context UI Item");
						ShowCall();
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
						ShowCall();
					}
				}
			}
			else if (GameManager.Instance.currentScene == GameManager.GameScene.GlobeScene)
			{
				HexCellData? hexCellData = GlobeInputManager.Instance.CurrentCell;
				//TODO Implement definition context menu
			}
		}
		else if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			if (!IsShown) return;

			var hoveredControl = GetViewport()?.GuiGetHoveredControl();

			// Check if the hovered control is part of the context menu hierarchy
			if (hoveredControl != null && !IsPartOfContextMenu(hoveredControl))
			{
				HideCall();
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
