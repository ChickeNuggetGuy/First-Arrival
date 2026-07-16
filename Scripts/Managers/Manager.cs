using Godot;

namespace FirstArrival.Scripts.Managers;

public abstract partial class Manager<T> : ManagerBase where T : ManagerBase, new()
{
	public static T Instance { get; private set; }
	private GameManager _subscribedGameManager;

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
		_subscribedGameManager = GameManager.Instance;
		if (_subscribedGameManager != null)
		{
			// Check if this node is a child of root (meaning it's an Autoload/Global)
			if (GetParent() == GetTree().Root)
			{
				_subscribedGameManager.RegisterGlobalManager(this);
			}
			
			_subscribedGameManager.SceneChanged += GameManagerOnSceneChanged;
		}
		
	}

	private void GameManagerOnSceneChanged(GameManager.GameScene scene)
	{
		if (Instance != null)
		{
			if (Instance != this)
			{
				this.QueueFree();
			}
		}
		else
		{
			Instance = this as T;
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
		// GameManager is persistent, so its C# event would otherwise retain this
		// scene-local manager after Godot has disposed it.
		if (_subscribedGameManager != null &&
		    GodotObject.IsInstanceValid(_subscribedGameManager))
		{
			_subscribedGameManager.SceneChanged -= GameManagerOnSceneChanged;
		}
		_subscribedGameManager = null;

		// Only clear the instance if this specific node was the active instance
		if (Instance == this)
		{
			Instance = null;
		}

		base._ExitTree();
	}
}
