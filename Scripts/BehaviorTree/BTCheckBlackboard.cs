using Godot;
using BehaviorTree.Core;

[GlobalClass]
public partial class BTCheckBlackboard : BTCondition
{
	[Export] public string Key { get; set; }
	[Export] public Variant ExpectedValue { get; set; }

	protected override bool Check()
	{
		if (!Blackboard.Has(Key)) return false;
        
		var val = Blackboard.Get(Key);
		
		return val.Obj == ExpectedValue.Obj; 
	}
}