using System.Collections.Generic;
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