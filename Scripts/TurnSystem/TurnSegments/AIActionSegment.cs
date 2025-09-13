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
                gridObject.ActionDefinitions != null && gridObject.ActionDefinitions.Length > 0).ToArray();

            foreach (GridObject activeGridObject in activeGridObjects)
            {
                GD.Print($"Processing actions for {activeGridObject.Name}");

                // Step 1: Find all possible actions and their best targets/scores/costs.
                var allPossibleActions = new List<(GridCell target, int score, ActionDefinition actionDefinition, Dictionary<Enums.Stat, int> costs)>();

                // Scan item actions
                var inventoriesToScan = new List<InventoryGrid>();
                if (activeGridObject.TryGetInventory(Enums.InventoryType.RightHand, out var rightHand)) inventoriesToScan.Add(rightHand);
                if (activeGridObject.TryGetInventory(Enums.InventoryType.LeftHand, out var leftHand)) inventoriesToScan.Add(leftHand);

                foreach (var inventory in inventoriesToScan)
                {
                    foreach (var item in inventory.uniqueItems)
                    {
                        if (item.ItemData.ActionDefinitions == null) continue;

                        foreach (var itemActionDef in item.ItemData.ActionDefinitions)
                        {
                            if (itemActionDef is ActionDefinition actionDefInstance && actionDefInstance is IItemActionDefinition itemActionInterface)
                            {
                                itemActionInterface.Item = item;
                                actionDefInstance.parentGridObject = activeGridObject;

                                var result = actionDefInstance.DetermineBestAIAction();
                                if (result.gridCell != null)
                                {
                                    allPossibleActions.Add((result.gridCell, result.score, actionDefInstance, result.costs));
                                }
                            }
                        }
                    }
                }

                // Scan non-item actions
                foreach (var action in activeGridObject.ActionDefinitions)
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
                    GD.Print($"No valid actions found for {activeGridObject.Name}. Skipping to next unit.");
                    continue; // Go to the next grid object
                }

                // Step 2: Sort all actions by score.
                allPossibleActions.Sort((a, b) => b.score.CompareTo(a.score));

                // Step 3: Execute actions from the sorted list as long as they are affordable.
                int actionsTaken = 0;
                foreach (var potentialAction in allPossibleActions)
                {
                    if (actionsTaken >= _maxActionAttempts)
                    {
                        GD.Print($"Max action limit reached for {activeGridObject.Name}.");
                        break;
                    }

                    // Check affordability again, as previous actions may have spent stats.
                    if (activeGridObject.CanAffordStatCost(potentialAction.costs))
                    {
                        GD.Print($"Executing action for {activeGridObject.Name}: {potentialAction.actionDefinition.GetActionName()} with score {potentialAction.score}");
                        await ExecuteAiAction(activeGridObject, potentialAction.target, potentialAction.score, potentialAction.actionDefinition);
                        actionsTaken++;
                    }
                }
                GD.Print($"Finished processing actions for {activeGridObject.Name}.");
            }

            return;
        }

        public async Task ExecuteAiAction(GridObject parent, GridCell target, int score, ActionDefinition actionDefinition)
        {
            GD.Print($"Attempting to take action: {actionDefinition.GetActionName()} for {parent.Name}");
            
            
            await ActionManager.Instance.TryTakeAction(actionDefinition, parent, parent.GridPositionData.GridCell, target);
            
        }
    }
}
