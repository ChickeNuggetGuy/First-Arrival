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
	private PanelContainer pageNavigation;
	private Button previousPageButton;
	private Button nextPageButton;
	private Label pageLabel;

	protected override Task _Setup()
	{
		InventoryManager.Instance.AddRuntimeInventoryGridUI(inventoryType, this);

		return Task.CompletedTask;
	}

	protected override async Task DrawUI()
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
			SetInventroyGrid(
				GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject
					.GridPositionData.AnchorCell.InventoryGrid
			);
		}

		SetupInventoryUI(InventoryGrid);

		base._Show();
	}


	public void SetupInventoryUI(InventoryGrid inventory)
	{
		DetachInventoryEvents();

		if (inventory != null)
			ClearSlots();
		else
		{
			GD.Print("Error: Inventory is null!!!");
			return;
		}

		InventoryGrid = inventory;
		if (InventoryGrid?.GridShape == null)
		{
			GD.PrintErr("Error: InventoryGrid or its GridShape is not assigned!");
			return;
		}
		if (InventoryGrid.Items == null)
			InventoryGrid.Initialize();
		if (InventoryGrid.Items == null) return;


		// Multi-cell icons are drawn as adjoining atlas slices. Any container spacing
		// becomes a visible cut through the reconstructed icon, so cells must touch.
		slotHolder.AddThemeConstantOverride("h_separation", 0);
		slotHolder.AddThemeConstantOverride("v_separation", 0);
		slotHolder.Columns = InventoryGrid.GridShape.SizeX;
		
		

		slotUIs = new ItemSlotUI[inventory.Items.GetLength(0), inventory.Items.GetLength(1)];
		GenerateGridSlots();
		EnsurePageNavigation();
		UpdatePageNavigation();

		InventoryGrid.InventoryChanged += OnInventoryChanged;
	}


	public void SetInventroyGrid(InventoryGrid inventory)
	{
		if (InventoryGrid != inventory)
			DetachInventoryEvents();
		InventoryGrid = inventory;
	}

	private void DetachInventoryEvents()
	{
		if (InventoryGrid != null)
			InventoryGrid.InventoryChanged -= OnInventoryChanged;
	}

	public override void _ExitTree()
	{
		DetachInventoryEvents();
		if (previousPageButton != null)
			previousPageButton.Pressed -= PreviousPage;
		if (nextPageButton != null)
			nextPageButton.Pressed -= NextPage;
		InventoryManager.Instance?.RemoveRuntimeInventoryGridUI(inventoryType, this);
		slotUIs = null;
		base._ExitTree();
	}

	private void ClearSlots()
	{
		foreach (Node child in slotHolder.GetChildren())
		{
			slotHolder.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void EnsurePageNavigation()
	{
		if (pageNavigation != null && GodotObject.IsInstanceValid(pageNavigation)) return;

		pageNavigation = new PanelContainer
		{
			Name = "PageNavigation",
			CustomMinimumSize = new Vector2(140, 28),
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 10
		};
		AddChild(pageNavigation);
		pageNavigation.SetAnchorsPreset(LayoutPreset.CenterBottom);
		pageNavigation.OffsetLeft = -70;
		pageNavigation.OffsetTop = -28;
		pageNavigation.OffsetRight = 70;
		pageNavigation.OffsetBottom = 0;

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		pageNavigation.AddChild(buttonRow);

		previousPageButton = new Button
		{
			Text = "<",
			TooltipText = "Previous inventory page",
			CustomMinimumSize = new Vector2(32, 24)
		};
		previousPageButton.Pressed += PreviousPage;
		buttonRow.AddChild(previousPageButton);

		pageLabel = new Label
		{
			CustomMinimumSize = new Vector2(64, 24),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		buttonRow.AddChild(pageLabel);

		nextPageButton = new Button
		{
			Text = ">",
			TooltipText = "Next inventory page",
			CustomMinimumSize = new Vector2(32, 24)
		};
		nextPageButton.Pressed += NextPage;
		buttonRow.AddChild(nextPageButton);
	}

	private void PreviousPage()
	{
		InventoryGrid?.SetCurrentPage(InventoryGrid.CurrentPageIndex - 1);
	}

	private void NextPage()
	{
		InventoryGrid?.SetCurrentPage(InventoryGrid.CurrentPageIndex + 1);
	}

	private void UpdatePageNavigation()
	{
		if (pageNavigation == null) return;

		bool showNavigation = InventoryGrid?.AllowsMultiplePages == true;
		pageNavigation.Visible = showNavigation;
		if (!showNavigation) return;

		pageLabel.Text = $"{InventoryGrid.CurrentPageIndex + 1} / {InventoryGrid.PageCount}";
		previousPageButton.Disabled = InventoryGrid.CurrentPageIndex == 0;
		nextPageButton.Disabled = InventoryGrid.CurrentPageIndex >= InventoryGrid.PageCount - 1;
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
				if (!InventoryGrid.GridShape.IsOccupied(x, 0, z))
				{
					Control blankSlot = InventoryManager.Instance.BlankSlotPrefab
						.Instantiate<Control>();
					blankSlot.CustomMinimumSize = new Vector2(20, 20);
					blankSlot.MouseFilter = MouseFilterEnum.Ignore;
					slotHolder.AddChild(blankSlot);
					continue;
				}

				ItemSlotUI newSlot = InventoryManager.Instance.InventorySlotPrefab
					.Instantiate<ItemSlotUI>();
				newSlot.Init(this, new Vector2I(x, z));

				if (InventoryGrid.TryGetItemAt(x, z, out var itemInfo) &&
				    itemInfo.item != null)
				{
					Vector2I rootPos = InventoryGrid.GetItemRootPos(itemInfo.item);
					Vector2I localCoords = new Vector2I(
						x - rootPos.X,
						z - rootPos.Y);
					bool isRoot = x == rootPos.X && z == rootPos.Y;

					newSlot.SetItem(itemInfo.item, itemInfo.count, localCoords, isRoot);
				}
				else
					newSlot.SetItem(null, 0, Vector2I.Zero, false);

				if (InventoryGrid.InventoryType == Enums.InventoryType.MouseHeld)
					newSlot.MouseFilter = MouseFilterEnum.Ignore;

				slotUIs[x, z] = newSlot;
				slotHolder.AddChild(newSlot);
			}
		}
	}

	public void UpdateSlotsUI()
	{
		if (!IsInsideTree() || InventoryGrid == null || slotUIs == null) return;

		for (int x = 0; x < slotUIs.GetLength(0); x++)
		{
			for (int z = 0; z < slotUIs.GetLength(1); z++)
			{
				ItemSlotUI currentSlot = slotUIs[x, z];
					if (currentSlot == null || !GodotObject.IsInstanceValid(currentSlot)) continue;

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
		MouseHeldInventoryUI mouseHeldInventory = UIManager.Instance.mouseHeldInventoryUI;

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
				mouseHeldInventory.previousInventory = InventoryGrid;
			}
			else if (slotItemInfo.item.IsRangedWeapon &&
			         slotItemInfo.item.CanReloadWith(mouseItemInfo.item, out _))
			{
				// Dropping a compatible ammo clip on a weapon performs a timed reload
				// action. The clip stays held if validation or payment fails.
				ActionManager.Instance?.RequestReload(slotItemInfo.item, mouseItemInfo.item);
			}
			else if (mouseItemInfo.item.ItemData.ItemID == slotItemInfo.item.ItemData.ItemID)
			{
				//Matching items. Drop 1 from hand to slot (Merging).
				InventoryGrid.TryTransferItem(mouseHeldInventory.InventoryGrid, InventoryGrid, mouseItemInfo.item, 1);

				if (!mouseHeldInventory.InventoryGrid.HasItemAt(0, 0))
				{
					mouseHeldInventory.HideCall();
					mouseHeldInventory.previousInventory = null;
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
		UpdatePageNavigation();
	}

	#endregion
}
