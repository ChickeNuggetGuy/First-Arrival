using System.Collections.Generic;
using System.Linq;
using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public abstract partial class ActionBase
{
  protected GridObject parentGridObject;
  protected GridCell startingGridCell;
  protected GridCell targetGridCell;
  protected ActionDefinition parentActionDefinition;
  public ActionBase NextActionBase {get; protected set;}

  protected Godot.Collections.Dictionary<Enums.Stat, int> costs = new();
  public ActionBase Parent { get; private set; } = null;
  private bool costsDeducted = false;

  protected void SetParent(ActionBase parent) => Parent = parent;

  protected void AddSubAction(ActionBase child)
  {
    if (child == null) return;
    child.SetParent(this);
    if (this is ICompositeAction composite)
    {
      composite.SubActions ??= new List<ActionBase>();
      composite.SubActions.Add(child);
    }
  }

  // Validation charges the parent action for a required turn. This queues the
  // matching child action with zero additional cost so execution always turns
  // before an attack, throw, or interaction is performed.
  protected bool AddRotateSubActionIfNeeded(
    GridCell facingFromCell,
    GridCell facingToCell,
    Enums.Direction assumedCurrentDirection = Enums.Direction.None,
    bool force = false
  )
  {
    if (
      this is not ICompositeAction
      || parentGridObject == null
      || facingFromCell == null
      || facingToCell == null
      || !parentGridObject.TryGetGridObjectNode<GridObjectActions>(
        out var gridObjectActions
      )
    )
      return false;

    var targetDirection = RotationHelperFunctions.GetDirectionBetweenCells(
      facingFromCell,
      facingToCell
    );
    if (targetDirection == Enums.Direction.None)
      return false;

    var currentDirection = assumedCurrentDirection == Enums.Direction.None
      ? parentGridObject.GridPositionData.Direction
      : assumedCurrentDirection;
    if (!force && currentDirection == targetDirection)
      return false;

    var rotateActionDefinition = gridObjectActions.ActionDefinitions?
      .FirstOrDefault(action => action is RotateActionDefinition)
      as RotateActionDefinition;
    if (rotateActionDefinition == null)
      return false;

    var rotateAction = rotateActionDefinition.InstantiateAction(
      parentGridObject,
      facingFromCell,
      facingToCell,
      new Godot.Collections.Dictionary<Enums.Stat, int>()
    );
    AddSubAction(rotateAction);
    return true;
  }

  public ActionBase() { }

  public ActionBase(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parent,
    Godot.Collections.Dictionary<Enums.Stat, int> costs
  )
  {
    this.parentActionDefinition = parent;
    this.parentGridObject = parentGridObject;
    this.startingGridCell = startingGridCell;
    this.targetGridCell = targetGridCell;
    this.costs = costs != null
      ? new Godot.Collections.Dictionary<Enums.Stat, int>(costs)
      : new Godot.Collections.Dictionary<Enums.Stat, int>();
  }

  public virtual async Task SetupCall()
  {
    if (this is ICompositeAction compositeAction)
    {
      compositeAction.SubActions ??= new List<ActionBase>();
      compositeAction.SubActions.Clear();
    }
    await Setup();
  }

  protected abstract Task Setup();

  public virtual async Task ExecuteCall()
  {
    await SetupCall();

    if (this is ICompositeAction compositeAction)
    {
	    for (int i = 0; i < compositeAction.SubActions.Count; i++)
	    {
		    if (i + 1 < compositeAction.SubActions.Count)
		    {
			    var action = compositeAction.SubActions[i];
			    action.SetNextAction(compositeAction.SubActions[i + 1]);
			    
		    }
	    }
	    
	    for (var index = 0; index < compositeAction.SubActions.Count; index++)
	    {
		    var action = compositeAction.SubActions[index];
		    await action.ExecuteCall();
	    }
    }

    await Execute();
    await ActionCompleteCall();
  }

  protected abstract Task Execute();

  protected virtual bool ShouldDeductCosts() => true;

  public async Task ActionCompleteCall()
  {
    await ActionComplete();
	
    if(!parentGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) return;
    
    if (!costsDeducted && ShouldDeductCosts())
    {
      foreach (var pair in costs)
      {
        if (!statHolder.TryGetStat(pair.Key, out var stat))
        {
          GD.Print($"Stat {pair.Key} not found");
          continue;
        }
        if (pair.Value != 0)
          stat.RemoveValue(pair.Value);
      }
      costsDeducted = true;
    }

    // IMPORTANT: pass both the definition and this action instance
    ActionManager.Instance.ActionCompleteCall(parentActionDefinition, this);
  }

  protected abstract Task ActionComplete();
  
  
  public void SetNextAction(ActionBase nextActionBase) => NextActionBase = nextActionBase;
}
