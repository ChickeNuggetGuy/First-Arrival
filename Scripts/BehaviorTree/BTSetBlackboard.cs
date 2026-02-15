using Godot;
using BehaviorTree.Core;

[GlobalClass]
public partial class BTSetBlackboard : BTNode
{
	[Export] public string Key { get; set; }
	[Export] public Variant Value { get; set; }

	public override BTStatus Tick(double delta)
	{
		if (string.IsNullOrEmpty(Key)) return BTStatus.Failure;
        
		Blackboard.Set(Key, Value);
		return BTStatus.Success;
	}
}