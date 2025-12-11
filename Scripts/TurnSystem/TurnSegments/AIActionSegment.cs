 using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Inventory_System;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.TurnSystem;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.AI
{
    [GlobalClass]
    public partial class AIActionSegment : TurnSegment 
    {
        GridObjectTeamHolder teamHolder;
        [Export] private int _maxActionAttempts = 5;

        public AIActionSegment()
        {
	        
        }
        protected override Task _Setup()
        {
            teamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(parentTurn.team);
            return Task.CompletedTask;
        }

        protected override async Task _Execute()
        {
            GD.Print("Executing AIActionSegment");
            GridObject[] activeGridObjects = teamHolder.GridObjects[Enums.GridObjectState.Active].Where(gridObject =>
                gridObject.TryGetGridObjectNode<GridObjectActions>(out var actions) != false && actions.ActionDefinitions.Length > 0).ToArray();

            foreach (GridObject activeGridObject in activeGridObjects)
            {
                GD.Print($"Processing actions for {activeGridObject.Name}");
                
                if(!activeGridObject.TryGetGridObjectNode<GridObjectActions>(out var gridObjectActions)) return;
                if(!activeGridObject.TryGetGridObjectNode<GridObjectStatHolder>(out GridObjectStatHolder statHolder)) continue;
                if (!activeGridObject.TryGetGridObjectNode<GridObjectInventory>(out var gridObjectInventory)) continue;
                for (int i = 0; i < _maxActionAttempts; i++)
                {
	                GD.Print($"Attempt {i + 1} of {_maxActionAttempts}");
                    // Step 1: Find all possible actions and their best targets/scores/costs for the current state.
                    var allPossibleActions = new List<(GridCell target, int score, ActionDefinition actionDefinition, Dictionary<Enums.Stat, int> costs)>();

                    // Scan item actions
                    var inventoriesToScan = new List<InventoryGrid>();
                    if (gridObjectInventory.TryGetInventory(Enums.InventoryType.RightHand, out var rightHand)) inventoriesToScan.Add(rightHand);
                    if (gridObjectInventory.TryGetInventory(Enums.InventoryType.LeftHand, out var leftHand)) inventoriesToScan.Add(leftHand);

                    foreach (var inventory in inventoriesToScan)
                    {
                        foreach (var itemInfo in inventory.UniqueItems)
                        {
                            if (itemInfo.item.ItemData.ActionDefinitions == null) continue;

                            foreach (var itemActionDef in itemInfo.item.ItemData.ActionDefinitions)
                            {
                                if (itemActionDef is ActionDefinition actionDefInstance && actionDefInstance is IItemActionDefinition itemActionInterface)
                                {
                                    itemActionInterface.Item = itemInfo.item;
                                    actionDefInstance.parentGridObject = activeGridObject;

                                    var result = actionDefInstance.DetermineBestAIAction();
                                    GD.Print($"Best AI Action was:{actionDefInstance.GetActionName()} : {result}");
                                    if (result.gridCell != null)
                                    {
                                        allPossibleActions.Add((result.gridCell, result.score, actionDefInstance, result.costs));
                                    }
                                }
                            }
                        }
                    }

                    // Scan non-item actions
                    foreach (var action in gridObjectActions.ActionDefinitions)
                    {
                        if (action is not IItemActionDefinition)
                        {
                            action.parentGridObject = activeGridObject;
                            var result = action.DetermineBestAIAction();
                            if (result.gridCell != null)
                            {
                                allPossibleActions.Add((result.gridCell, result.score, action, result.costs));
                            }
                        }
                    }

                    if (allPossibleActions.Count == 0)
                    {
                        GD.Print($"No valid actions found for {activeGridObject.Name}. Ending its turn.");
                        break; 
                    }

                    // Step 2: Sort all actions by score and pick the best one.
                    allPossibleActions.Sort((a, b) => b.score.CompareTo(a.score));
                    var bestAction = allPossibleActions.First();

                    if (bestAction.score <= 0)
                    {
                        GD.Print($"No more actions with score > 0 for {activeGridObject.Name}. Ending its actions.");
                        break;
                    }

                    // Step 3: Execute the best action if affordable.
                    if (statHolder.CanAffordStatCost(bestAction.costs))
                    {
                        GD.Print($"Executing action for {activeGridObject.Name}: {bestAction.actionDefinition.GetActionName()} with score {bestAction.score}");
                        await ExecuteAiAction(activeGridObject, bestAction.target, bestAction.score, bestAction.actionDefinition);
                    }
                    else
                    {
                        GD.Print($"Cannot afford best action {bestAction.actionDefinition.GetActionName()}. Ending turn for {activeGridObject.Name}.");
                        break; 
                    }
                }
                GD.Print($"Finished processing actions for {activeGridObject.Name}.");
            }

            return;
        }

        public async Task ExecuteAiAction(GridObject parent, GridCell target, int score, ActionDefinition actionDefinition)
        {
            GD.Print($"Attempting to take action: {actionDefinition.GetActionName()} for {parent.Name}");

            var actionCompletedSignal = ActionManager.Instance.ToSignal(ActionManager.Instance, ActionManager.SignalName.ActionCompleted);
            if (await ActionManager.Instance.TryTakeAction(actionDefinition, parent, parent.GridPositionData.GridCell, target))
            {
                await actionCompletedSignal;
            }
        }
    }
}
