#if TOOLS
using Godot;

[Tool]
public partial class GridShapeEditorPlugin : EditorPlugin
{
	// Keeping it static helps prevent duplicate instances, 
	// but we must manage it carefully.
	private static GridShapeInspectorPlugin _inspectorPlugin;

	public override void _EnterTree()
	{
		// 1. Always clean up existing instances first to be safe
		if (_inspectorPlugin != null)
		{
			RemoveInspectorPlugin(_inspectorPlugin);
			_inspectorPlugin.Free(); // Ensure memory is freed
			_inspectorPlugin = null;
		}

		// 2. Get the UndoRedo manager
		var undoRedo = GetUndoRedo();
		if (undoRedo == null)
		{
			GD.PrintErr("GridShapeEditorPlugin: Failed to get EditorUndoRedoManager.");
			return;
		}

		// 3. Create a FRESH instance with the valid dependency
		_inspectorPlugin = new GridShapeInspectorPlugin(undoRedo);
        
		// 4. Register it
		AddInspectorPlugin(_inspectorPlugin);
		GD.Print("GridShapeEditorPlugin: Inspector plugin registered.");
	}

	public override void _ExitTree()
	{
		if (_inspectorPlugin != null)
		{
			RemoveInspectorPlugin(_inspectorPlugin);
			// It's good practice to free the resource explicitly if we created it
			// providing it isn't being held by the engine elsewhere.
			// However, removing it is usually enough for GC/RefCounted.
			_inspectorPlugin = null; 
		}
		GD.Print("GridShapeEditorPlugin: Inspector plugin cleaned up.");
	}
}
#endif