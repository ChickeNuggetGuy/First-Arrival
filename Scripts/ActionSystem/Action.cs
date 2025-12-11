using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public abstract partial class Action
{
  protected GridObject parentGridObject;
  protected GridCell startingGridCell;
  protected GridCell targetGridCell;
  protected ActionDefinition parentActionDefinition;

  protected Dictionary<Enums.Stat, int> costs = new();
  public Action Parent { get; private set; } = null;
  private bool costsDeducted = false;

  protected void SetParent(Action parent) => Parent = parent;

  protected void AddSubAction(Action child)
  {
    if (child == null) return;
    child.SetParent(this);
    if (this is ICompositeAction composite)
    {
      composite.SubActions ??= new List<Action>();
      composite.SubActions.Add(child);
    }
  }

  public Action() { }

  public Action(
    GridObject parentGridObject,
    GridCell startingGridCell,
    GridCell targetGridCell,
    ActionDefinition parent,
    Dictionary<Enums.Stat, int> costs
  )
  {
    this.parentActionDefinition = parent;
    this.parentGridObject = parentGridObject;
    this.startingGridCell = startingGridCell;
    this.targetGridCell = targetGridCell;
    this.costs = costs != null
      ? new Dictionary<Enums.Stat, int>(costs)
      : new Dictionary<Enums.Stat, int>();
  }

  public virtual async Task SetupCall()
  {
    if (this is ICompositeAction compositeAction)
    {
      compositeAction.SubActions ??= new List<Action>();
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
      foreach (Action action in compositeAction.SubActions)
        await action.ExecuteCall();
    }

    await Execute();
    await ActionCompleteCall();
  }

  protected abstract Task Execute();

  protected virtual bool ShouldDeductCosts() => Parent == null;

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
}