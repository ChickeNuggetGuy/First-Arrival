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
  private ActionBase activeSubAction;
  private Task cancellationTask;
  private bool visibilityInterruptRequested;

  public bool IsCancellationRequested { get; private set; }
  public bool WasInterruptedByNewEnemy { get; private set; }
  public GridObject ActingGridObject => parentGridObject;

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
    try
    {
      await SetupCall();
      if (IsCancellationRequested) return;

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
		      if (IsCancellationRequested) return;

		      var action = compositeAction.SubActions[index];
          activeSubAction = action;
          try
          {
		        await action.ExecuteCall();
          }
          finally
          {
            if (ReferenceEquals(activeSubAction, action))
              activeSubAction = null;
          }

          if (IsCancellationRequested) return;
          if (visibilityInterruptRequested)
          {
            // The completed child has already committed its state (for
            // example, a movement step has entered its new cell). Cancel only
            // after it returns so cancellation cannot roll that state back.
            await CancelCall();
            return;
          }
          if (!ShouldContinueAfterSubAction(action))
            break;
	      }
      }

      if (IsCancellationRequested) return;
      if (visibilityInterruptRequested)
      {
        await CancelCall();
        return;
      }
      await Execute();
      if (IsCancellationRequested) return;
      await ActionCompleteCall();
    }
    finally
    {
      if (Parent == null && IsCancellationRequested)
      {
        try
        {
          await (cancellationTask ?? CancelCall());
        }
        finally
        {
          ActionManager.Instance?.ActionCanceledCall(parentActionDefinition, this);
        }
      }
    }
  }

  protected abstract Task Execute();

  /// <summary>
  /// Lets a composite action stop its remaining children after inspecting the
  /// child that just completed.
  /// </summary>
  protected virtual bool ShouldContinueAfterSubAction(ActionBase completedSubAction)
  {
    return true;
  }

  /// <summary>
  /// Requests cancellation for this action and its currently executing child.
  /// Override ActionCanceled to restore any action-specific transient state.
  /// </summary>
  public Task CancelCall()
  {
    if (cancellationTask != null)
      return cancellationTask;

    IsCancellationRequested = true;
    cancellationTask = CancelInternal();
    return cancellationTask;
  }

  /// <summary>
  /// Stops a composite action at its next safe child boundary when the action
  /// reveals a previously unseen hostile. The current child is allowed to
  /// finish first, so a completed movement step is never rolled back.
  /// </summary>
  public void RequestVisibilityInterrupt()
  {
    WasInterruptedByNewEnemy = true;
    visibilityInterruptRequested = true;

    if (this is ICompositeAction)
      activeSubAction?.RequestVisibilityInterrupt();
  }

  private async Task CancelInternal()
  {
    ActionBase child = activeSubAction;
    if (child != null)
      await child.CancelCall();

    await ActionCanceled();
  }

  protected virtual Task ActionCanceled() => Task.CompletedTask;

  /// <summary>
  /// Waits for a tween while allowing a cancellation-aware action to stop it.
  /// Returns false when cancellation interrupted the tween.
  /// </summary>
  protected async Task<bool> WaitForTween(Tween tween)
  {
    if (tween == null) return false;

    while (GodotObject.IsInstanceValid(tween) && tween.IsRunning())
    {
      if (IsCancellationRequested)
      {
        tween.Kill();
        return false;
      }

      SceneTree tree = parentGridObject?.GetTree();
      if (tree == null) return false;

      await parentGridObject.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    return !IsCancellationRequested;
  }

  /// <summary>
  /// Applies the global action-animation multiplier to a tween. A multiplier
  /// of 2 plays the tween twice as fast without changing its authored timing.
  /// </summary>
  protected Tween ApplyAnimationSpeed(Tween tween)
  {
    if (tween == null)
      return null;

    tween.SetSpeedScale(
      SettingsManager.Instance?.AnimationSpeedMultiplier ?? 1.0f
    );
    return tween;
  }

  /// <summary>
  /// Returns true when this action's visuals can actually be seen. Player
  /// units bypass fog-of-war, but all units must be inside the active camera's
  /// viewport. Non-player units must also occupy a currently visible cell.
  /// </summary>
  protected bool ShouldAnimate()
  {
    if (parentGridObject == null || !GodotObject.IsInstanceValid(parentGridObject))
      return false;

    GridCell visibilityCell = startingGridCell
      ?? parentGridObject.GridPositionData?.AnchorCell;
    bool isPlayerUnit = parentGridObject.Team == Enums.UnitTeam.Player;

    if (
      !isPlayerUnit
      && (visibilityCell == null
        || visibilityCell.fogState != Enums.FogState.Visible)
    )
      return false;

    Viewport viewport = parentGridObject.GetViewport();
    Camera3D camera = viewport?.GetCamera3D()
      ?? CameraController.Instance?.MainCamera;
    if (viewport == null || camera == null || !GodotObject.IsInstanceValid(camera))
      return false;

    Vector3 worldPosition = parentGridObject.objectCenter?.GlobalPosition
      ?? parentGridObject.GlobalPosition;
    if (camera.IsPositionBehind(worldPosition))
      return false;

    // A small margin prevents animation popping when the unit is touching the
    // viewport edge but its origin has just moved outside the exact rectangle.
    Rect2 screenRect = viewport.GetVisibleRect().Grow(32.0f);
    return screenRect.HasPoint(camera.UnprojectPosition(worldPosition));
  }

  protected virtual bool ShouldDeductCosts() => true;

  public async Task ActionCompleteCall()
  {
    if (IsCancellationRequested) return;

    await ActionComplete();

    if (IsCancellationRequested) return;
	
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
