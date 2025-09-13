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
                GD.Print($"Trying to take action for {activeGridObject.Name}");
                int attemptCount = 0;
                bool actionTaken = false;

                while (attemptCount < _maxActionAttempts && !actionTaken)
                {

                    List<(GridCell target, int score, ActionDefinition actionDefinition)> actionScores = new();
                    
                    List<InventoryGrid> inventoriesToScan = new();
                    if (activeGridObject.TryGetInventory(Enums.InventoryType.RightHand, out var rightHand))
                    {
                        inventoriesToScan.Add(rightHand);
                    }
                    if (activeGridObject.TryGetInventory(Enums.InventoryType.LeftHand, out var leftHand))
                    {
                        inventoriesToScan.Add(leftHand);
                    }

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
                                        actionScores.Add((result.gridCell, result.score, actionDefInstance));
                                    }
                                }
                            }
                        }
                    }

                    // --- Get Non-Item Actions (like Move) ---
                    foreach (var action in activeGridObject.ActionDefinitions)
                    {
                        if (action is not IItemActionDefinition)
                        {
                            var result = action.DetermineBestAIAction();
                            if (result.gridCell != null)
                            {
                                actionScores.Add((result.gridCell, result.score, action));
                            }
                        }
                    }

                    if (actionScores.Count == 0)
                    {
                        GD.Print($"No valid actions found for {activeGridObject.Name} on attempt {attemptCount + 1}. Total attempts: {attemptCount + 1}/{_maxActionAttempts}");
                        attemptCount++;
                        if (attemptCount >= _maxActionAttempts)
                        {
                            GD.Print($"Max action attempts reached for {activeGridObject.Name}. Skipping to next unit.");
                            break; // Exit retry loop for this unit
                        }
                    }
                    else
                    {
                        // Sort actions by score descending
                        actionScores.Sort((a, b) => b.score.CompareTo(a.score));

                        var bestAction = actionScores[0];

                        GD.Print($"Best action for {activeGridObject.Name} is {bestAction.actionDefinition.GetActionName()} with score {bestAction.score}");
                        
                        await ExecuteAiAction(activeGridObject, bestAction.target, bestAction.score, bestAction.actionDefinition);
                        actionTaken = true;
                    }
                }
            }
        }

        public async Task ExecuteAiAction(GridObject parent, GridCell target, int score, ActionDefinition actionDefinition)
        {
            GD.Print($"Attempting to take action: {actionDefinition.GetActionName()} for {parent.Name}");
            
            
            await ActionManager.Instance.TryTakeAction(actionDefinition, parent, parent.GridPositionData.GridCell, target);
            
        }
    }
}