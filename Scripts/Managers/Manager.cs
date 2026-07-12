using Godot;

namespace FirstArrival.Scripts.Managers;

public abstract partial class Manager<T> : ManagerBase where T : ManagerBase, new()
{
	public static T Instance { get; private set; }

	[Export] public bool IsBusy { get; set; }
	


	/// <summary>
	/// If true, this instance will replace any existing instance in the singleton slot.
	/// Useful for scene-local managers that should refresh on scene load.
	/// </summary>
	[Export] public bool overridePreviousInstance = true;

	public override void _Ready()
	{
		base._Ready();
		Instance = this as T;
		AddToGroup("Manager");
		if (GameManager.Instance != null)
		{
			// Check if this node is a child of root (meaning it's an Autoload/Global)
			if (GetParent() == GetTree().Root)
			{
				GameManager.Instance.RegisterGlobalManager(this);
			}
		}
	}

	public virtual void SetIsBusy(bool isBusy)
	{
		IsBusy = isBusy;
		if (DebugMode)
			GD.Print($"{GetManagerName()}: IsBusy set to {isBusy}");
	}

	public override void _ExitTree()
	{
		// Only clear the instance if this specific node was the active instance
		if (Instance == this)
		{
			Instance = null;
		}
	}
}