using System;

namespace FirstArrival.Scripts.Utility;

public class Enums
{
	#region Grid System Enums
	public enum GridObjectState
	{
		Active,
		Inactive,
	}
	
	[System.Flags]
	public enum GridObjectSettings
	{
		None = 0,
		CanWalkThrough,
	}
	[System.Flags]
	public enum GridCellState
	{
		None = 0,
		Disabled = 1,
		Enabled = 2,
		Obstructed = 16,
		Empty = 32,
		Ground = 64,
		Air = 128,
		RootNode = 256,
		DoorNode = 512,
		
	}
	public enum FogState
	{
		Unseen, 
		PreviouslySeen,
		Visible
	}
	
	public enum Direction
	{
		None,
		North,
		NorthEast,
		East,
		SouthEast,
		South,
		SouthWest,
		West,
		NorthWest,
	}

	#endregion
	
	[Flags]
	public enum UnitTeam
	{
		None = 0,
		Player = 1,
		Enemy = 2,
		Neutral = 4,
		All = Player | Enemy | Neutral,
	}

	public enum Stat
	{
		None = 0,
		Health,
		Stamina,
		Bravery,
		TimeUnits,
		RangedAccuracy
	}

	public enum StatTurnBehavior
	{
		None = 0,
		ResetToMax,
		ResetToMin,
		Increment,
		Decrement
	}
	
	#region Inventory system
	public enum InventoryType
	{
		None = 0,
		Ground,
		Backpack,
		Belt,
		QuickDraw,
		LeftHand,
		RightHand,
		leftLeg,
		rightLeg,
		MouseHeld,
	}

	[Flags]
	public enum InventorySettings
	{
		None = 0,
		IsEquipmentinventory = 1,
		UseItemSizes = 2,
		CanExecuteActions = 4,
		MaxItemAmount = 8,
		MaxWeight = 16,
		AllowItemStacking = 32
		
	}

	[Flags]
	public enum ItemSettings
	{
		None = 0,
		CanMelee = 1 << 0,
		CanRanged = 1 << 1,
		CanThrow = 1 << 2,
		CanEquip = 1 << 3,
	}
	#endregion

	#region UI Enums

	public enum UIAnimationType
	{
		Translate,
		Scale,
		Rotate,
	}
	#endregion
	
	public enum InventoyShapeData
	{
		Enabled,
		Disabled
	}
	
	
	public enum HexGridType {Land, Water}
	
	public enum MissionType {None, Eliminate, Survive, Objective, Timed}


	#region Animation

	public enum LocomotionType
	{
		None = 0,
		Idle = 1,
		Moving = 2,
		InAir = 4,
	}

	public enum Stance
	{
		None = 0,
		Normal = 1,
		Crouched = 2,
		Prone = 4,
		Sprinting = 8
	}

	#endregion
}