 using Godot;
using System;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class Obstacle : GridCellStateOverride
{
	public override void _EnterTree()
	{
		base._EnterTree();
		AddToGroup("GridObjects");
	}

	public override void _Ready()
	{
		if (collisionShape != null)
			collisionShape.CollisionMask = PhysicsLayer.OBSTACLE;
		
		useGridCellStateOverride = true;
		cellStateOverride = Enums.GridCellState.Obstructed;
		cellStateOverrideFilter = Enums.GridCellState.None;
		base._Ready();
	}
}
