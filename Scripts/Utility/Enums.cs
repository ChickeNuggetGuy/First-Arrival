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
	public enum GridCellState
	{
		None = 0,
		Walkable = 1,
		Unwalkable = 2,
		Obstructed = 4,
		Empty = 8,
		Ground = 16,
		Air = 32,
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
		TimeUnits
	}
	
	
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
		isEquipmentinventory,
		UseItemSizes
	}
}