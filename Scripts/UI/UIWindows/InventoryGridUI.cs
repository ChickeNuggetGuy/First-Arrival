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
	public InventoryGrid InventoryGrid{get; protected set;}
	[Export] private GridContainer slotHolder;


	private ItemSlotUI[,] slotUIs;
	protected override Task _Setup()
	{
		InventoryManager.Instance.AddRuntimeInventoryGridUI(inventoryType, this);
		GridObject gridObject =
			GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject;


		if (gridObject == null)
		{
			GD.Print("Error: GridObject is null!");
			return Task.CompletedTask;
		}

		if (!gridObject.TryGetGridObjectNode<GridObjectInventory>(out var gridObjectInventory)) return Task.CompletedTask;
		if (inventoryType == Enums.InventoryType.Ground)
		{
			if (InventoryGrid == null)
			{
				GridCell currentGridCell = gridObject.GridPositionData.AnchorCell;
				if (currentGridCell == null || currentGridCell.InventoryGrid == null) return Task.CompletedTask;
				InventoryGrid = currentGridCell.InventoryGrid;
			}
		}
		else
		{
			if (InventoryGrid == null)
			{
				if (!gridObjectInventory.TryGetInventory(inventoryType, out var inventory))
				{
					GD.Print("Error: gridObjectInventory is null!");
					return Task.CompletedTask;
				}
				GD.Print(inventory.InventoryType + " Name");

				InventoryGrid = inventory;

			}
		}

		SetupInventoryUI(InventoryGrid);
		InventoryGrid.InventoryChanged += InventoryOnInventoryChanged;

		return base._Setup();
	}

	private void InventoryOnInventoryChanged()
	{
		UpdateSlotsUI();
	}

	protected override void _Show()
	{
		UpdateSlotsUI();
		if (inventoryType == Enums.InventoryType.Ground)
		{
			SetupInventoryUI(GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject.GridPositionData.AnchorCell.InventoryGrid);
		}
		
		base._Show();
	}

	
	public void SetupInventoryUI(InventoryGrid inventory)
	{
		if (InventoryGrid != null)
		{
			InventoryGrid.InventoryChanged -= InventoryOnInventoryChanged;
		}
		if(inventory != null)
			ClearSlots();
		
		InventoryGrid = inventory;
		if (InventoryGrid == null)
		{
			GD.PrintErr("InventoryGrid resource is not assigned!");
			return;
		}
		
		
		// Now it's safe to access InventoryShape properties.
		slotHolder.Columns = InventoryGrid.GridShape.GridSizeX;
		
		slotUIs = new ItemSlotUI[inventory.Items.GetLength(0), inventory.Items.GetLength(1)];
		GenerateGridSlots();

		// Connect to the signal to listen for future changes.
		InventoryGrid.InventoryChanged += OnInventoryChanged;
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

        for (int y = 0; y < InventoryGrid.GridShape.GridSizeZ; y++)
        {
            for (int x = 0; x < InventoryGrid.GridShape.GridSizeX; x++)
            {
                ItemSlotUI newSlot;
                if (InventoryGrid.GridShape.GetGridShapeCell(x,0, y))
                {
                    // Use the prefab for a real slot
                    newSlot = (ItemSlotUI)InventoryManager.Instance.InventorySlotPrefab.Instantiate<Control>();
                    (Item item, int count) item = (null, 0);
                    
                    InventoryGrid.TryGetItemAt(x,y, out item);
                    newSlot.SetItem(item.item,item.count, new Vector2I(x, y));
                    newSlot.Init(this, new Vector2I(x,y));
                    slotUIs[x, y] = newSlot;

                    if (InventoryGrid.InventoryType == Enums.InventoryType.MouseHeld)
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
                
                if (InventoryGrid.TryGetItemAt(currentCoords.X,  currentCoords.Y, out var item))
                {
                    currentSlot.SetItem(item.item, item.count, currentCoords);
                }
                else
                {
                    currentSlot.SetItem(null,0, currentCoords);
                }
            }
        }
    }

    public void ItemSlot_Pressed(ItemSlotUI slotPressed)
    {
        MouseHeldInventoryUI mouseHeldInventory = InventoryManager.Instance.mouseHeldInventoryUI;
        if (InventoryGrid.TryGetItemAt(slotPressed.inventoryCoords.X, slotPressed.inventoryCoords.Y, out var itemInfo))
        {
            //Item at slot, should be picked up by MouseHeld slot if empty
            if (mouseHeldInventory.InventoryGrid.TryGetItemAt(0, 0, out var mouseHeldItemInfo))
            {
                //MouseHeldInventory has Item, nothing can be done
                return;
            }
            else
            {
                //MouseHeldInventory does not have an Item, "pickup" item from clicked slot
                if (!InventoryGrid.TryTransferItem(InventoryGrid, mouseHeldInventory.InventoryGrid, itemInfo.item, itemInfo.count))
                {
                    return;
                }
                mouseHeldInventory.ShowCall();
                
            }
        }
        else
        {
            //Clicked Slot is empty, Check if MouseHeld slot has item. If so place that item.
            if (mouseHeldInventory.InventoryGrid.TryGetItemAt(0, 0, out var mouseHeldItemInfo))
            {
                if (!InventoryGrid.TryTransferItemAt(mouseHeldInventory.InventoryGrid,new Vector2I(0,0) , InventoryGrid,slotPressed.inventoryCoords, out Item transferedItem))
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
	    UpdateSlotsUI();
    }

    #endregion
}