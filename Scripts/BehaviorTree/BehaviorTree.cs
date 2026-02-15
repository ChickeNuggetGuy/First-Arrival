using Godot;

namespace BehaviorTree.Core;

[GlobalClass, Icon("res://addons/behavior_tree/bt_icon.svg")]
public partial class BehaviorTree : BTNode
{
	public GridObject ParentGridObject => GetParent() as GridObject;
	[Export] public bool AutoInitialize { get; set; } = true;

	[Export] public bool RestartOnTick { get; set; } = true;

	private bool _initialized;
	private BTNode _root;
	private BTStatus _lastStatus = BTStatus.Success;

	public override void _Ready()
	{
		Blackboard ??= new Blackboard();

		foreach (var child in GetBTChildren())
		{
			_root = child;
			break;
		}

		if (_root == null)
		{
			GD.PushWarning($"BehaviorTree '{Name}' has no BTNode children.");
			return;
		}

		if (AutoInitialize)
			InitializeTree();
	}

	public void InitializeTree()
	{
		if (_initialized) return;
		_initialized = true;

		Agent ??= GetParent();
		
		Tree = this;

		PropagateSetup(_root);
	}

	private void PropagateSetup(BTNode node)
	{
		node.Tree = this;
		node.Blackboard = Blackboard;
		node.Agent = Agent;
		node.Initialize();

		foreach (var child in node.GetChildren())
		{
			if (child is BTNode btChild)
				PropagateSetup(btChild);
		}
	}

	public BTStatus TickTree(double delta = 0.0)
	{
		if (_root == null) return BTStatus.Failure;

		if (RestartOnTick && _lastStatus != BTStatus.Running)
		{
			// Full restart each turn
		}

		_lastStatus = _root.Tick(delta);
		return _lastStatus;
	}

	public override BTStatus Tick(double delta) => TickTree(delta);
}