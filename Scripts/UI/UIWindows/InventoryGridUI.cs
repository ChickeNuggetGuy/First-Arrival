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
	public InventoryGrid InventoryGrid { get; protected set; }
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
		if (InventoryGrid == null)
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
				InventoryGrid = InventoryManager.Instance.GetInventoryGrid(Enums.InventoryType.Ground);
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
			InventoryGrid = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject
				.GridPositionData.AnchorCell.InventoryGrid;
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

		if (inventory != null)
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


		slotHolder.Columns = InventoryGrid.GridShape.SizeX;
		
		

		slotUIs = new ItemSlotUI[inventory.Items.GetLength(0), inventory.Items.GetLength(1)];
		GenerateGridSlots();

		InventoryGrid.InventoryChanged += OnInventoryChanged;
	}


	public void SetInventroyGrid(InventoryGrid inventory)
	{
		InventoryGrid = inventory;
	}

	private void ClearSlots()
	{
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

		for (int z = 0; z < InventoryGrid.GridShape.SizeZ; z++)
		{
			for (int x = 0; x < InventoryGrid.GridShape.SizeX; x++)
			{
				ItemSlotUI newSlot = (ItemSlotUI)InventoryManager.Instance.InventorySlotPrefab.Instantiate<Control>();

				if (InventoryGrid.GridShape.IsOccupied(x, 0, z))
				{
					newSlot.Init(this, new Vector2I(x, z));

					if (InventoryGrid.TryGetItemAt(x, z, out var itemInfo) && itemInfo.item != null)
					{
						Vector2I rootPos = InventoryGrid.GetItemRootPos(itemInfo.item);
						Vector2I localCoords = new Vector2I(x - rootPos.X, z - rootPos.Y);
						bool isRoot = (x == rootPos.X && z == rootPos.Y);

						newSlot.SetItem(itemInfo.item, itemInfo.count, localCoords, isRoot);
					}
					else
					{
						newSlot.SetItem(null, 0, Vector2I.Zero, false);
					}

					if (InventoryGrid.InventoryType == Enums.InventoryType.MouseHeld)
						newSlot.MouseFilter = MouseFilterEnum.Ignore;
				}
				else
				{
					newSlot.Disabled = true;
				}

				slotUIs[x, z] = newSlot;
				slotHolder.AddChild(newSlot);
			}
		}
	}

	public void UpdateSlotsUI()
	{
		for (int x = 0; x < slotUIs.GetLength(0); x++)
		{
			for (int z = 0; z < slotUIs.GetLength(1); z++)
			{
				ItemSlotUI currentSlot = slotUIs[x, z];
				if (currentSlot == null) continue;

				if (InventoryGrid.TryGetItemAt(x, z, out var itemInfo) && itemInfo.item != null)
				{
					Vector2I rootPos = InventoryGrid.GetItemRootPos(itemInfo.item);

					Vector2I localCoords = new Vector2I(x - rootPos.X, z - rootPos.Y);

					bool isRoot = (x == rootPos.X && z == rootPos.Y);

					currentSlot.SetItem(itemInfo.item, itemInfo.count, localCoords, isRoot);
				}
				else
				{
					currentSlot.SetItem(null, 0, Vector2I.Zero, false);
				}
			}
		}
	}

	public void ItemSlot_Pressed(ItemSlotUI slotPressed)
	{
		MouseHeldInventoryUI mouseHeldInventory = InventoryManager.Instance.mouseHeldInventoryUI;

		// Check what's in the clicked slot
		bool slotHasItem =
			InventoryGrid.TryGetItemAt(slotPressed.inventoryCoords.X, slotPressed.inventoryCoords.Y,
				out var slotItemInfo) && slotItemInfo.item != null;

		// Check what's in the mouse inventory
		bool mouseHasItem = mouseHeldInventory.InventoryGrid.TryGetItemAt(0, 0, out var mouseItemInfo) &&
		                    mouseItemInfo.item != null;

		if (slotHasItem)
		{
			if (!mouseHasItem)
			{
				//Pick up 1 item from slot to empty mouse
				InventoryGrid.TryTransferItem(InventoryGrid, mouseHeldInventory.InventoryGrid, slotItemInfo.item, 1);
				mouseHeldInventory.ShowCall();
			}
			else if (mouseItemInfo.item.ItemData.ItemID == slotItemInfo.item.ItemData.ItemID)
			{
				//Matching items. Drop 1 from hand to slot (Merging).
				InventoryGrid.TryTransferItem(mouseHeldInventory.InventoryGrid, InventoryGrid, mouseItemInfo.item, 1);

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
				// Place all mouse items into empty slot
				if (InventoryGrid.TryTransferItemAt(mouseHeldInventory.InventoryGrid, new Vector2I(0, 0), InventoryGrid,
					    slotPressed.inventoryCoords, out Item transferredItem))
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