using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class ModifyStatAction : ActionBase, ICompositeAction
{
	private bool _targetWasModified;
	private bool _setupSucceeded;

	public ActionBase ParentActionBase { get; set; }
	public List<ActionBase> SubActions { get; set; }

	public ModifyStatAction(
		GridObject parentGridObject,
		GridCell startingGridCell,
		GridCell targetGridCell,
		ModifyStatActionDefinition parentAction,
		Godot.Collections.Dictionary<Enums.Stat, int> costs
	) : base(parentGridObject, startingGridCell, targetGridCell, parentAction, costs)
	{
	}

	protected override Task Setup()
	{
		ParentActionBase = this;
		_setupSucceeded = false;

		if (parentActionDefinition is not ModifyStatActionDefinition definition)
		{
			GD.PushError("ModifyStatAction: parent definition is invalid");
			return Task.CompletedTask;
		}

		if (definition.targetRequirement != ModifyStatTargetRequirement.Adjacency)
		{
			_setupSucceeded = true;
			return Task.CompletedTask;
		}

		GridCell currentCell = parentGridObject?.GridPositionData?.AnchorCell
			?? startingGridCell;
		if (definition.IsWithinAdjacencyRange(currentCell, targetGridCell))
		{
			_setupSucceeded = true;
			return Task.CompletedTask;
		}

		if (!definition.TryBuildMoveToTargetAdjacency(
			parentGridObject,
			currentCell,
			targetGridCell,
			out MoveActionDefinition moveDefinition,
			out GridCell moveDestination,
			out _,
			out string reason
		))
		{
			GD.PrintErr($"ModifyStatAction: could not move into range: {reason}");
			return Task.CompletedTask;
		}

		ActionBase moveAction = moveDefinition.InstantiateAction(
			parentGridObject,
			currentCell,
			moveDestination,
			new Godot.Collections.Dictionary<Enums.Stat, int>()
		);
		if (moveAction == null)
		{
			GD.PrintErr("ModifyStatAction: failed to create move sub-action");
			return Task.CompletedTask;
		}

		AddSubAction(moveAction);

		// MoveAction delegates its costs to its MoveStep children. Keep only this
		// action's own cost on the parent so movement is not charged twice.
		costs.Clear();
		costs[Enums.Stat.TimeUnits] = definition.timeUnitCost;
		costs[Enums.Stat.Stamina] = definition.staminaCost;
		_setupSucceeded = true;
		return Task.CompletedTask;
	}

	protected override Task Execute()
	{
		if (!_setupSucceeded)
			return Task.CompletedTask;

		if (parentActionDefinition is not ModifyStatActionDefinition definition)
		{
			GD.PushError("ModifyStatAction: parent definition is invalid");
			return Task.CompletedTask;
		}

		if (
			definition.targetRequirement == ModifyStatTargetRequirement.Adjacency
			&& !definition.IsWithinAdjacencyRange(
				parentGridObject?.GridPositionData?.AnchorCell,
				targetGridCell
			)
		)
		{
			GD.Print("ModifyStatAction: acting unit did not reach the target");
			return Task.CompletedTask;
		}

		if (!definition.TryGetTargetGridObject(
			parentGridObject,
			targetGridCell,
			out GridObject targetGridObject
		))
		{
			GD.Print("ModifyStatAction: no valid target remains in the target cell");
			return Task.CompletedTask;
		}

		if (!targetGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out var statHolder))
			return Task.CompletedTask;

		foreach (var modifier in definition.targetStats)
		{
			if (modifier.Key == Enums.Stat.None || modifier.Value == 0)
				continue;

			if (!statHolder.TryGetStat(modifier.Key, out GridObjectStat stat))
				continue;

			stat.AddValue(modifier.Value);
			_targetWasModified = true;
		}

		return Task.CompletedTask;
	}

	protected override Task ActionComplete()
	{
		return Task.CompletedTask;
	}

	// Do not charge the acting unit if the target disappeared or stopped being
	// valid between validation and execution.
	protected override bool ShouldDeductCosts() => _targetWasModified;
}
