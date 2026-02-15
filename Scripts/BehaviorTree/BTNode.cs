using Godot;

namespace BehaviorTree.Core;

[GlobalClass]
public abstract partial class BTNode : Node
{
	public BehaviorTree Tree { get; set; }

	public Blackboard Blackboard { get; set; }

	public Node Agent { get; set; }

	public virtual void Initialize() { }

	public abstract BTStatus Tick(double delta);

	public virtual void Abort()
	{
		foreach (var child in GetBTChildren())
		{
			child.Abort();
		}
	}

	protected System.Collections.Generic.IEnumerable<BTNode> GetBTChildren()
	{
		foreach (var child in GetChildren())
		{
			if (child is BTNode btChild)
				yield return btChild;
		}
	}
}