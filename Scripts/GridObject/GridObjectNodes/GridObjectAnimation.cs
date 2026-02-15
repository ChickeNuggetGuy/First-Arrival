using Godot;
using System;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class GridObjectAnimation : GridObjectNode
{
	[Export] AnimationTree animationTree;
	[Export] AnimationPlayer animationPlayer;
	[Export] public bool isMoving;
	[Export] public bool isIdle;
	[Export]public Enums.WeaponState WeaponState { get; protected set; } = Enums.WeaponState.None;
	
	
	protected override void Setup()
	{
		isMoving = false;
		isIdle = true;
		WeaponState = Enums.WeaponState.None;
	}

	public override Dictionary<string, Variant> Save()
	{
		Dictionary<string, Variant> data =  new Dictionary<string, Variant>();
		
		data.Add("isMoving", isMoving.ToString());
		data.Add("isIdle", isIdle.ToString());
		return data;
	}

	public override void Load(Dictionary<string, Variant> data)
	{
		if (data.TryGetValue("isIdle", out Variant locomotionType))
		{
			isIdle = locomotionType.AsBool();
		}
		
		if (data.TryGetValue("isMoving", out Variant movingData))
		{
			isMoving = movingData.AsBool();
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

	public void SetLocomotionType(Enums.LocomotionType locomotionType) {

		GD.Print("Test: SetLocomotionType");
		switch (locomotionType)
		{
			case Enums.LocomotionType.None:
				break;
			case Enums.LocomotionType.Idle:
				isIdle = true;
				isMoving = false;
				break;
			case Enums.LocomotionType.Moving:
				isMoving = true;
				isIdle = false;
				break;
			case Enums.LocomotionType.InAir:
				break;
				isIdle = false;
			default:
				throw new ArgumentOutOfRangeException(nameof(locomotionType), locomotionType, null);
		}
	}
	
	public void AddWeaponState(Enums.WeaponState weaponState)
	{
		if(WeaponState.HasFlag(weaponState)) return;
		this.WeaponState |= weaponState;
		GD.Print($"unit now has {weaponState.ToString()}");
	}
	
	public void RemoveWeaponState(Enums.WeaponState weaponState)
	{
		if(!WeaponState.HasFlag(weaponState)) return;
		this.WeaponState &= ~weaponState;
		GD.Print($"unit no longer has {weaponState.ToString()}");
	}

	#endregion
}
