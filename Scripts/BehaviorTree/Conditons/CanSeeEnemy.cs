using Godot;
using System;
using System.Linq;
using BehaviorTree.Core;

[GlobalClass]
public partial class CanSeeEnemy : BTCondition
{
	[Export] public bool desiredResult = true;
	[Export] public int  desiredCount;


	protected override bool Check()
	{
		if(Tree.ParentGridObject == null) return false;
		
		if(!Tree.ParentGridObject.TryGetGridObjectNode<GridObjectSight>(out GridObjectSight sight)) return false;
		
		
		if (desiredResult)
		{
			//Returns true if enemies can be seen
			if (sight.SeenGridObjects.Where(gridObject => 
				    gridObject.Team != Tree.ParentGridObject.Team && !gridObject.scenery).ToArray().Length >= desiredCount)
			{
				return true;
			}
			
		}
		else
		{
			//Returns false if enemies can be seen
			if (sight.SeenGridObjects.Where(gridObject =>
				    gridObject.Team != Tree.ParentGridObject.Team && !gridObject.scenery).ToArray().Length <= desiredCount)
			{
				return true;
			}
		}
		return false;
	}
}
