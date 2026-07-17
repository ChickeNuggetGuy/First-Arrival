using Godot;
using System.Linq;
using BehaviorTree.Core;

[GlobalClass]
public partial class CanSeeEnemy : BTCondition
	{
		[Export] public bool desiredResult = true;
		[Export] public int desiredCount = 1;


	protected override bool Check()
	{
		if(Tree.ParentGridObject == null) return false;
		
		if(!Tree.ParentGridObject.TryGetGridObjectNode<GridObjectSight>(out GridObjectSight sight)) return false;

		sight.EnsureUpToDate();

		int enemyCount = sight.SeenGridObjects.Count(gridObject =>
			gridObject != null
			&& gridObject.IsActive
			&& gridObject.Team != Tree.ParentGridObject.Team
			&& !gridObject.scenery
		);

		return desiredResult
			? enemyCount >= desiredCount
			: enemyCount < desiredCount;
	}
}
