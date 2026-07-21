#if TOOLS
using Godot;

[Tool]
public partial class CountryDatabaseEditorPlugin : EditorPlugin
{
	private CountryDatabaseEditorWindow _window;

	public override void _EnterTree()
	{
		_window = new CountryDatabaseEditorWindow();
		_window.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_window.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_window.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		EditorInterface.Singleton.GetEditorMainScreen().AddChild(_window);
		_window.Visible = false;
	}

	public override void _ExitTree()
	{
		if (_window == null)
			return;

		if (_window.GetParent() != null)
			_window.GetParent().RemoveChild(_window);

		_window.QueueFree();
		_window = null;
	}

	public override bool _HasMainScreen() => true;

	public override void _MakeVisible(bool visible)
	{
		if (_window != null)
		{
			_window.Visible = visible;
			if (visible)
				_window.EnsureLoaded();
		}
	}

	public override string _GetPluginName() => "Country DB";

	public override Texture2D _GetPluginIcon()
	{
		return EditorInterface.Singleton.GetBaseControl()
			.GetThemeIcon("WorldEnvironment", "EditorIcons");
	}
}
#endif
