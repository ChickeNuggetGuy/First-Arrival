using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.UI;

[GlobalClass]
public partial class InventoryGridUI : UIWindow
{
	[Export] Enums.InventoryType inventoryType;
	public InventoryGrid inventoryGrid{get; protected set;}
	[Export] private GridContainer slotHolder;


	private ItemSlotUI[,] slotUIs;
	protected override Task _Setup()
	{
		InventoryManager.Instance.AddRuntimeInventoryGridUI(inventoryType, this);
		GridObject gridObject =
			GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject;


		if (gridObject == null) return Task.CompletedTask;
		if (inventoryType == Enums.InventoryType.Ground)
		{
			if (inventoryGrid == null)
			{

				GridCell currentGridCell = gridObject.GridPositionData.GridCell;
				if (currentGridCell == null || currentGridCell.InventoryGrid == null) return Task.CompletedTask;
				inventoryGrid = currentGridCell.InventoryGrid;
			}
		}
		else
		{


			if (inventoryGrid == null)
			{
				if (!gridObject.TryGetInventory(inventoryType, out var inventory)) return Task.CompletedTask;
				GD.Print(inventory.InventoryType + " Name");

				inventoryGrid = inventory;

			}
		}

		SetupInventoryUI(inventoryGrid);
		inventoryGrid.InventoryChanged += InventoryOnInventoryChanged;

		return base._Setup();
	}

	private void InventoryOnInventoryChanged()
	{
		UpdateSlotsUI();
	}

	protected override void _Show()
	{
		GridObject gridObject =
			GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject;
		
		if (gridObject != null) return;
		
		if(!gridObject.TryGetInventory(inventoryType, out var inventory)) return;
		
		
		base._Show();
	}

	
	public void SetupInventoryUI(InventoryGrid inventory)
	{
		if(inventory != null)
			ClearSlots();
		
		inventoryGrid = inventory;
		if (inventoryGrid == null)
		{
			GD.PrintErr("InventoryGrid resource is not assigned!");
			return;
		}
		
		
		// Now it's safe to access InventoryShape properties.
		slotHolder.Columns = inventoryGrid.GridShape.GridWidth;
		
		slotUIs = new ItemSlotUI[inventory.Itemstems.GetLength(0), inventory.Itemstems.GetLength(1)];
		GenerateGridSlots();

		// Connect to the signal to listen for future changes.
		inventoryGrid.InventoryChanged += OnInventoryChanged;
	}

	private void ClearSlots()
	{
		// Clear any old slots before generating new ones
		foreach (Node child in slotHolder.GetChildren())
		{
			child.QueueFree();
		}

	}
	/// <summary>
    /// Clears and rebuilds the visual grid based on the inventory's shape.
    /// </summary>
    private void GenerateGridSlots()
    {
		ClearSlots();

        for (int y = 0; y < inventoryGrid.GridShape.GridHeight; y++)
        {
            for (int x = 0; x < inventoryGrid.GridShape.GridWidth; x++)
            {
                ItemSlotUI newSlot;
                if (inventoryGrid.GridShape.GetGridShapeCell(x, y))
                {
                    // Use the prefab for a real slot
                    newSlot = (ItemSlotUI)InventoryManager.Instance.InventorySlotPrefab.Instantiate<Control>();
                    Item itemOrNull = null;
                    
                    inventoryGrid.TryGetItemAt(x,y, out itemOrNull);
                    newSlot.SetItem(itemOrNull, new Vector2I(x, y));
                    newSlot.Init(this, new Vector2I(x,y));
                    slotUIs[x, y] = newSlot;

                    if (inventoryGrid.InventoryType == Enums.InventoryType.MouseHeld)
                    {
	                    newSlot.MouseFilter = MouseFilterEnum.Ignore;
                    }
                }
                else
                {
                    // Use the prefab for a blank/disabled slot
                    newSlot = (ItemSlotUI)InventoryManager.Instance.InventorySlotPrefab.Instantiate<Control>();
                    slotUIs[x, y] = newSlot;
                }
                slotHolder.AddChild(newSlot);
            }
        }
    }

    public void UpdateSlotsUI()
    {
        for (int x = 0; x < slotUIs.GetLength(0); x++)
        {
            for (int y = 0; y < slotUIs.GetLength(1); y++)
            {
                ItemSlotUI currentSlot = slotUIs[x, y];
                Vector2I currentCoords = new Vector2I(x, y);
                
                if (inventoryGrid.TryGetItemAt(currentCoords.X,  currentCoords.Y, out Item item))
                {
                    currentSlot.SetItem(item, currentCoords);
                }
                else
                {
                    currentSlot.SetItem(null, currentCoords);
                }
            }
        }
    }

    public void ItemSlot_Pressed(ItemSlotUI slotPressed)
    {
        MouseHeldInventoryUI mouseHeldInventory = InventoryManager.Instance.mouseHeldInventoryUI;
        if (inventoryGrid.TryGetItemAt(slotPressed.inventoryCoords.X, slotPressed.inventoryCoords.Y, out Item item))
        {
            //Item at slot, should be picked up by MouseHeld slot if empty
            if (mouseHeldInventory.inventoryGrid.TryGetItemAt(0, 0, out Item mouseHeldItem))
            {
                //MouseHeldInventory has Item, nothing can be done
                return;
            }
            else
            {
                //MouseHeldInventory does not have an Item, "pickup" item from clicked slot
                if (!InventoryGrid.TryTransferItem(inventoryGrid, mouseHeldInventory.inventoryGrid, item))
                {
                    return;
                }
                mouseHeldInventory.ShowCall();
                
            }
        }
        else
        {
            //Clicked Slot is empty, Check if MouseHeld slot has item. If so place that item.
            if (mouseHeldInventory.inventoryGrid.TryGetItemAt(0, 0, out Item mouseHeldItem))
            {
                if (!InventoryGrid.TryTransferItemAt(mouseHeldInventory.inventoryGrid,new Vector2I(0,0) , inventoryGrid,slotPressed.inventoryCoords, out Item transferedItem))
                {
                    return;
                }
                mouseHeldInventory.HideCall();
                
                return;
            }
            else
            {
                return;
            }
        }
    }
    
    
    #region EventHandlers

    /// <summary>
    /// This function is called whenever the inventory data changes.
    /// </summary>
    private void OnInventoryChanged()
    {
	    GD.Print("Inventory has changed. UI needs to update!");
	    UpdateSlotsUI();
    }

    #endregion
}