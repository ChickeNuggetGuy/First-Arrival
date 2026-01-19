using Godot;
using System;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class GridObjectAnimation : GridObjectNode
{
	[Export] AnimationTree animationTree;
	[Export] AnimationPlayer animationPlayer;
	public Enums.LocomotionType LocomotionType { get; protected set; } = Enums.LocomotionType.None;
	protected override void Setup()
	{
		LocomotionType = Enums.LocomotionType.Idle;
	}

	public override Dictionary<string, Variant> Save()
	{
		Dictionary<string, Variant> data =  new Dictionary<string, Variant>();
		
		data.Add("LocomotionType", LocomotionType.ToString());
		return data;
	}

	public override void Load(Dictionary<string, Variant> data)
	{
		if (data.TryGetValue("LocomotionType", out Variant locomotionType))
		{
			
			LocomotionType = Enum.Parse<Enums.LocomotionType>(locomotionType.ToString());
		}
	}

	#region Animation Functions

	public bool TrySetParameter(string parameterName, Variant value)
	{
		if (animationTree == null) return false;
		
		var parameter = animationTree.Get("parameters/" + parameterName);

		if (parameter.VariantType != value.VariantType) return false;
		
		animationTree.Set("parameters/" + parameterName, value);
		return true;
	}

	#endregion

	#region Get/Set Functions

	public void SetLocomotionType(Enums.LocomotionType locomotionType) => this.LocomotionType = locomotionType;

	#endregion
}
