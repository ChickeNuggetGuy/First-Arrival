using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
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
		Dictionary<String,Callable> callables = contextUser.GetContextActions();
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
		
		contextButton.Init(callable, name);
		contextButtons.Add(contextButton);
		contextButtonHolder.AddChild(contextButton);
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Right &&
		    mouseEvent.Pressed)
		{
			//Right Mouse Button was pressed, Check if the object preseed has IContextUser
			GodotObject obj = InputManager.Instance.GetObjectAtMousePosition(out Vector3 hitPosition);

			if (obj == null) return;

			if (obj is IContextUserBase contextUser)
			{
				if (!TryGenerateContextMenu(contextUser)) return;
				ShowCall();
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