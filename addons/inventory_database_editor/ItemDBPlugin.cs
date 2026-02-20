#if TOOLS
using Godot;
using System;

[Tool]
public partial class ItemDBPlugin : EditorPlugin
{
	private ItemDBWindow _window;

	public override void _EnterTree()
	{
		_window = new ItemDBWindow();
        
		// Essential: Make sure the window fills the editor area
		_window.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_window.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_window.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		EditorInterface.Singleton.GetEditorMainScreen().AddChild(_window);
		_window.Visible = false;
	}

	public override void _ExitTree()
	{
		if (_window != null)
		{
			// CHANGED: Do NOT call RemoveControlFromDocks. 
			// Since we added it to MainScreen, we just remove it from parent.
			if (_window.GetParent() != null)
			{
				_window.GetParent().RemoveChild(_window);
			}
			_window.QueueFree();
		}
	}

	public override bool _HasMainScreen()
	{
		return true;
	}

	public override void _MakeVisible(bool visible)
	{
		if (_window != null)
		{
			_window.Visible = visible;
		}
	}

	public override string _GetPluginName()
	{
		return "Item DB";
	}

	public override Texture2D _GetPluginIcon()
	{
		return EditorInterface.Singleton.GetBaseControl().GetThemeIcon("ResourcePreloader", "EditorIcons");
	}
}
#endif