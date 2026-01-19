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
	[Export] public Enums.InventoryType inventoryType;
	public InventoryGrid InventoryGrid{get; protected set;}
	[Export] private GridContainer slotHolder;
	[Export] public bool AutoFetchGround { get; set; } = true;


	private ItemSlotUI[,] slotUIs;
	protected override Task _Setup()
	{
		InventoryManager.Instance.AddRuntimeInventoryGridUI(inventoryType, this);
		
		

		return base._Setup();
	}

	private void InventoryOnInventoryChanged()
	{
		UpdateSlotsUI();
	}

	protected override void _Show()
	{
		if(InventoryGrid == null)
		{
			GridObject gridObject = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player)
				.CurrentGridObject;


			if (gridObject == null)
			{
				GD.Print("Error: GridObject is null!!!");
				return;
			}

			if (!gridObject.TryGetGridObjectNode<GridObjectInventory>(out var gridObjectInventory)) return;
			if (inventoryType == Enums.InventoryType.Ground)
			{
				// Ground inventory logic handled below to ensure update on show
			}
			else
			{
				if (InventoryGrid != null)
				{
					InventoryGrid.InventoryChanged -= InventoryOnInventoryChanged;
				}

				if (!gridObjectInventory.TryGetInventory(inventoryType, out var inventory))
				{
					GD.Print("Error: gridObjectInventory is null!");
					return;
				}
				

				InventoryGrid = inventory;
			}
		}
		
		if (inventoryType == Enums.InventoryType.Ground && AutoFetchGround)
		{
			InventoryGrid = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject.GridPositionData.AnchorCell.InventoryGrid;
		}

		SetupInventoryUI(InventoryGrid);
		
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
		else
		{
			GD.Print("Error: Inventory is null!!!");
			return;
		}
		
		InventoryGrid = inventory;
		if (InventoryGrid == null)
		{
			GD.PrintErr("Error: InventoryGrid resource is not assigned!");
			return;
		}
		
		
		// Now it's safe to access InventoryShape properties.
		slotHolder.Columns = InventoryGrid.GridShape.GridSizeX;
		
		slotUIs = new ItemSlotUI[inventory.Items.GetLength(0), inventory.Items.GetLength(1)];
		GenerateGridSlots();

		// Connect to the signal to listen for future changes.
		InventoryGrid.InventoryChanged += OnInventoryChanged;
	}


	public void SetInventroyGrid(InventoryGrid inventory)
	{
		InventoryGrid = inventory;
	}
	private void ClearSlots()
	{
		// Clear any old slots before generating new ones
		foreach (Node child in slotHolder.GetChildren())
		{
			slotHolder.RemoveChild(child);
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
                    newSlot.Init(this, new Vector2I(x,y));
                    (Item item, int count) item = (null, 0);
                    
                    InventoryGrid.TryGetItemAt(x,y, out item);
                    newSlot.SetItem(item.item,item.count, new Vector2I(x, y));
  
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
                    newSlot.Disabled = true;
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
        
        // Check what's in the clicked slot
        bool slotHasItem = InventoryGrid.TryGetItemAt(slotPressed.inventoryCoords.X, slotPressed.inventoryCoords.Y, out var slotItemInfo) && slotItemInfo.item != null;
        
        // Check what's in the mouse inventory
        bool mouseHasItem = mouseHeldInventory.InventoryGrid.TryGetItemAt(0, 0, out var mouseItemInfo) && mouseItemInfo.item != null;

        if (slotHasItem)
        {
	        if (!mouseHasItem)
	        {
		        // Case: Pick up 1 item from slot to empty mouse
		        InventoryGrid.TryTransferItem(InventoryGrid, mouseHeldInventory.InventoryGrid, slotItemInfo.item, 1);
		        mouseHeldInventory.ShowCall();
	        }
	        else if (mouseItemInfo.item.ItemData.ItemID == slotItemInfo.item.ItemData.ItemID)
	        {
		        // Case: Matching items. Drop 1 from hand to slot (Merging).
		        InventoryGrid.TryTransferItem(mouseHeldInventory.InventoryGrid, InventoryGrid, mouseItemInfo.item, 1);
		        
		        // If mouse is now empty, hide it
		        if (!mouseHeldInventory.InventoryGrid.HasItemAt(0, 0))
		        {
			        mouseHeldInventory.HideCall();
		        }
	        }
	        else 
	        {
		        // Different items, do nothing
		        return;
	        }
        }
        else
        {
	        // Slot is empty
	        if (mouseHasItem)
	        {
		        // Case: Place ALL mouse items into empty slot
		        if (InventoryGrid.TryTransferItemAt(mouseHeldInventory.InventoryGrid, new Vector2I(0, 0), InventoryGrid, slotPressed.inventoryCoords, out Item transferredItem))
		        {
			        // If mouse is now empty, hide it
			        if (!mouseHeldInventory.InventoryGrid.HasItemAt(0, 0))
			        {
				        mouseHeldInventory.HideCall();
			        }
		        }
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