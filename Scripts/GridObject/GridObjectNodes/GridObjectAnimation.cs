using Godot;
using System;
using FirstArrival.Scripts.Managers;
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
	private static readonly System.Collections.Generic.HashSet<ulong>
		NormalizedLocomotionAnimations = new();
	
	
	protected override void Setup()
	{
		NormalizeLocomotionRootTranslation();
		ApplyAnimationSpeed();
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

	/// <summary>
	/// Keeps imported walk/run clips in place. The GridObject itself owns world
	/// movement; horizontal translation on the Hips track would otherwise be
	/// added visually and then snap back when the state machine returns to idle.
	/// </summary>
	private void NormalizeLocomotionRootTranslation()
	{
		if (animationPlayer == null) return;

		foreach (StringName animationName in animationPlayer.GetAnimationList())
		{
			string name = animationName.ToString();
			if (!IsLocomotionAnimation(name)) continue;

			Animation animation = animationPlayer.GetAnimation(animationName);
			if (animation == null ||
			    !NormalizedLocomotionAnimations.Add(animation.GetInstanceId()))
				continue;

			for (int trackIndex = 0;
			     trackIndex < animation.GetTrackCount();
			     trackIndex++)
			{
				if (animation.TrackGetType(trackIndex) !=
				    Animation.TrackType.Position3D)
					continue;

				string trackPath = animation.TrackGetPath(trackIndex).ToString();
				if (!trackPath.EndsWith(":Hips", StringComparison.OrdinalIgnoreCase))
					continue;

				int keyCount = animation.TrackGetKeyCount(trackIndex);
				if (keyCount == 0) continue;

				Vector3 anchor = animation
					.TrackGetKeyValue(trackIndex, 0)
					.AsVector3();
				for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
				{
					Vector3 position = animation
						.TrackGetKeyValue(trackIndex, keyIndex)
						.AsVector3();
					position.X = anchor.X;
					position.Z = anchor.Z;
					animation.TrackSetKeyValue(trackIndex, keyIndex, position);
				}
			}
		}
	}

	private static bool IsLocomotionAnimation(string animationName)
	{
		return animationName.StartsWith("walk_", StringComparison.OrdinalIgnoreCase) ||
		       animationName.StartsWith("run_", StringComparison.OrdinalIgnoreCase) ||
		       animationName.StartsWith("sprint_", StringComparison.OrdinalIgnoreCase);
	}

	private void ApplyAnimationSpeed()
	{
		if (animationPlayer != null)
		{
			animationPlayer.SpeedScale =
				SettingsManager.Instance?.AnimationSpeedMultiplier ?? 1.0f;
		}
	}

	public bool TrySetParameter(string parameterName, Variant value)
	{
		if (animationTree == null)
			return false;

		string path = "parameters/" + parameterName;

		Variant current;
		try
		{
			current = animationTree.Get(path);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Invalid AnimationTree parameter path: {path}");
			GD.PrintErr(e.Message);
			return false;
		}

		if (current.VariantType == Variant.Type.Nil)
		{
			GD.PrintErr($"AnimationTree parameter does not exist: {path}");
			return false;
		}

		if (current.VariantType != value.VariantType)
		{
			GD.PrintErr(
				$"Type mismatch for {path}. Expected {current.VariantType}, got {value.VariantType}"
			);
			return false;
		}

		animationTree.Set(path, value);
		return true;
	}

	#endregion

	#region Get/Set Functions

	public void SetLocomotionType(Enums.LocomotionType locomotionType) {
		ApplyAnimationSpeed();

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
