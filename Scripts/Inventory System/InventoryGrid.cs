using System.Collections.Generic;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Inventory_System;

[GlobalClass]
public partial class InventoryGrid : Resource
{
	[Export] public Enums.InventoryType InventoryType { get; protected set; }

	[Export] public GridShape GridShape { get; protected set; }
	[Export] public bool UseItemSize { get; protected set; } = true;
	[Export] public Enums.InventorySettings inventorySettings { get; protected set; }
	public Item[,] Itemstems{get; private set;}

	/// <summary>
	/// Gets the nummber of unique Items in the grid
	/// </summary>
	public int ItemCount
	{
		get
		{
			if (Itemstems == null) return -1;
			List<Item> uniqueItems = new  List<Item>();

			for (int x = 0; x < Itemstems.GetLength(0); x++)
			{
				for (int y = 0; y < Itemstems.GetLength(1); y++)
				{
					if  (Itemstems[x, y] == null) continue;
					if (uniqueItems.Contains(Itemstems[x, y])) continue;
					uniqueItems.Add(Itemstems[x, y]);
				}
			}
			return uniqueItems.Count;
		}
	}

	public List<Item> uniqueItems
	{
		get
		{
			List<Item> uniqueItems = new  List<Item>();

			for (int x = 0; x < Itemstems.GetLength(0); x++)
			{
				for (int y = 0; y < Itemstems.GetLength(1); y++)
				{
					if  (Itemstems[x, y] == null) continue;
					if (uniqueItems.Contains(Itemstems[x, y])) continue;
					uniqueItems.Add(Itemstems[x, y]);
				}
			}
			return uniqueItems;
		}
	} 
	#region Signals

	[Signal]
	public delegate void InventoryChangedEventHandler();

	[Signal]
	public delegate void ItemAddedEventHandler(Item itemAdded);

	[Signal]
	public delegate void ItemRemovedEventHandler(Item itemREmoved); // Note: Typo in "itemREmoved"

	#endregion

	#region Functions

	public void Initialize()
	{
		Itemstems = new Item[GridShape.GridWidth, GridShape.GridHeight];
	}

	private void AddItem(Item item)
	{
		if (item == null) return;

		if (!CanAddItem(item, out var position)) return;
		AddItemAt(position, item);
	}
	
	private void AddItemAt(Vector2I position, Item item) => AddItemAt(position.X, position.Y, item);

	private void AddItemAt(int x, int y, Item item)
	{
		// --- Corrected Logic ---
		if (UseItemSize)
		{
			// An item can occupy multiple cells based on its ItemShape.
			GridShape itemShape = item?.ItemData?.ItemShape;

			// Safety check in case item or its data/shape is null/missing
			// If no shape is defined, perhaps default to placing it in the single cell (x,y).
			// This depends on your game's rules. For now, we'll assume a shape is needed if UseItemSize=true,
			// and CanAddItemAt should have validated this. If not, we place it in (x,y) if valid.
			if (itemShape == null)
			{
				// Fallback or error handling. Placing in single cell as a default.
				GD.PushWarning($"AddItemAt: Item '{item?.ItemData?.ItemName ?? "Unknown"}' has no ItemShape but UseItemSize is true. Placing in single cell ({x},{y}).");
				if (x >= 0 && x < GridShape.GridWidth && y >= 0 && y < GridShape.GridHeight && GridShape.GetGridShapeCell(x, y))
				{
					Itemstems[x, y] = item;
				}
			}
			else
			{
				// Place the item in all cells defined by its ItemShape, offset by (x, y).
				for (int relX = 0; relX < itemShape.GridWidth; relX++)
				{
					for (int relY = 0; relY < itemShape.GridHeight; relY++)
					{
						// Check if this part of the item's shape is actually occupied/used.
						if (!itemShape.GetGridShapeCell(relX, relY))
						{
							continue; // Skip unused cells within the item's shape bounds.
						}

						int gridX = x + relX;
						int gridY = y + relY;

						// Bounds check (should ideally be done in CanAddItemAt, but safe here as a safeguard)
						// Also check if the calculated grid cell is part of the inventory's shape.
						if (gridX >= 0 && gridX < GridShape.GridWidth && gridY >= 0 && gridY < GridShape.GridHeight && GridShape.GetGridShapeCell(gridX, gridY))
						{
							Itemstems[gridX, gridY] = item;
						}
						// Optionally, else log error if CanAddItemAt was supposed to prevent this.
						// else { GD.PrintErr($"AddItemAt: Calculated position ({gridX},{gridY}) is out of bounds or invalid for inventory shape."); }
					}
				}
			}
		}
		else
		{
			// --- Corrected Logic for UseItemSize = false ---
			// Place the item reference only in the single specified cell (x, y).

			// Bounds and shape check (should be ensured by CanAddItemAt, but safe here as a safeguard)
			if (x >= 0 && x < GridShape.GridWidth && y >= 0 && y < GridShape.GridHeight && GridShape.GetGridShapeCell(x, y))
			{
				Itemstems[x, y] = item;
			}
			// Optionally, else log error if CanAddItemAt was supposed to prevent this.
			// else { GD.PrintErr($"AddItemAt: Position ({x},{y}) is out of bounds or invalid for inventory shape (UseItemSize=false)."); }
		}
		// --- End of Correction ---

		EmitSignal(SignalName.ItemAdded, item);
		EmitSignal(SignalName.InventoryChanged);
	}

	public bool TryAddItem(Item item)
	{
		if (!CanAddItem(item, out Vector2I position))
			return false;

		AddItemAt(position.X, position.Y, item);
		return true;
	}

	public bool TryAddItemAt(Item item, Vector2I position)
	{
		// Delegate the core logic to the existing CanAddItemAt and AddItemAt(int, int, Item) functions.
		// This ensures consistent validation and addition behavior.
		if (CanAddItemAt(position.X, position.Y, item))
		{
			// If validation passes, proceed to add the item at the specified location.
			AddItemAt(position.X, position.Y, item);
			return true; // Indicate successful addition.
		}
		// If CanAddItemAt returned false, the item cannot be placed at the given position.
		// Reasons might include: out of bounds, overlaps with existing item, invalid inventory shape cell, etc.
		// The function implicitly returns false in this case.
		return false; // Indicate failure to add.
	}
	
	private void RemoveItem(Item item)
	{
		if (item == null) return;
		List<Vector2I> positions = GetItemPositions(item);
		foreach (Vector2I pos in positions)
		{
			Itemstems[pos.X, pos.Y] = null;
		}

		EmitSignal(SignalName.ItemRemoved, item);
		EmitSignal(SignalName.InventoryChanged);
	}

	public bool TryRemoveItem(Item item)
	{
		if (!HasItem(item)) return false;
		RemoveItem(item);
		return true;
	}

	public bool CanAddItem(Item item, out Vector2I position)
	{
		position = new Vector2I(-1, -1);
		if (item == null) return false;
		// TODO: Check stacking logic

		GridShape itemShape = item.ItemData?.ItemShape;

		// Iterate through potential top-left corners in the inventory grid
		for (int x = 0; x <= GridShape.GridWidth - (UseItemSize && itemShape != null ? itemShape.GridWidth : 1); x++)
		{
			for (int y = 0; y <= GridShape.GridHeight - (UseItemSize && itemShape != null ? itemShape.GridHeight : 1); y++)
			{
				// The logic for checking if an item fits is now consolidated in CanAddItemAt.
				if (CanAddItemAt(x, y, item)) 
				{
					position = new Vector2I(x, y);
					return true;
				}
			}
		}

		return false;
	}

	public bool CanAddItemAt(int x, int y, Item item)
	{
		if (item == null) return false;
		// TODO: Check stacking logic

		// --- Corrected/Clarified Logic ---
		if (UseItemSize)
		{
			// An item can occupy multiple cells based on its ItemShape.
			GridShape itemShape = item?.ItemData?.ItemShape;

			// Safety check: If UseItemSize is true but item has no shape, it cannot be placed this way.
			if (itemShape == null)
			{
				GD.PushWarning($"CanAddItemAt: Item '{item?.ItemData?.ItemName ?? "Unknown"}' has no ItemShape but UseItemSize is true. Cannot place.");
				return false;
			}

			// Check each cell that the item would occupy
			for (int relX = 0; relX < itemShape.GridWidth; relX++)
			{
				for (int relY = 0; relY < itemShape.GridHeight; relY++)
				{
					// Check if this part of the item's shape is actually used/occupied.
					if (!itemShape.GetGridShapeCell(relX, relY))
					{
						continue; // This part of the item's bounding box is empty, skip it.
					}

					int gridX = x + relX;
					int gridY = y + relY;

					// 1. Check if the calculated position is within the inventory grid bounds
					if (gridX < 0 || gridX >= GridShape.GridWidth || gridY < 0 || gridY >= GridShape.GridHeight)
					{
						return false; // Part of the item would be placed outside the inventory grid.
					}

					// 2. Check if the calculated cell is valid according to the inventory's own GridShape
					//    (Important if the inventory grid itself is not a perfect rectangle)
					if (!GridShape.GetGridShapeCell(gridX, gridY))
					{
						return false; // This cell in the inventory is not part of the usable space.
					}

					// 3. Check if the cell in the inventory grid is already occupied
					//    Use direct check for null. Consistent with HasItemAt's final check.
					if (Itemstems[gridX, gridY] != null)
					{
						return false; // Cell is already occupied by another item.
					}
					// Note on Inventory Shape Check: The check `GridShape.GetGridShapeCell(gridX, gridY)`
					// above ensures we are only trying to place items in valid inventory cells.
					// The original `HasItemAt` also did this check, so this is the correct place for it.
				}
			}
		}
		else
		{
			// --- Corrected Logic for UseItemSize = false ---
			// An item occupies only the single cell (x, y).

			// 1. Check if the position (x, y) is within the inventory grid bounds
			if (x < 0 || x >= GridShape.GridWidth || y < 0 || y >= GridShape.GridHeight)
			{
				return false; // Position is outside the grid dimensions.
			}

			// 2. Check if the cell (x, y) is valid according to the inventory's own GridShape
			//    (Important if the inventory grid itself is not a perfect rectangle)
			if (!GridShape.GetGridShapeCell(x, y))
			{
				return false; // This cell is not part of the usable inventory space.
			}

			// 3. Check if the cell (x, y) in the inventory grid is already occupied
			//    Use direct check for null, consistent with HasItemAt's final check.
			if (Itemstems[x, y] != null)
			{
				return false; // Cell is already occupied.
			}
		}
		// --- End of Correction ---

		return true; // All necessary checks for placement passed.
	}

    // ... (rest of the class remains the same) ...

	public bool HasItem(Item item)
	{
		if (item == null) return false;
		if (Itemstems == null) return false;

		for (int x = 0; x < GridShape.GridWidth; x++)
		{
			for (int y = 0; y < GridShape.GridHeight; y++)
			{
				if (Itemstems[x, y] == item) return true;
			}
		}

		return false;
	}

	public bool HasItemAt(int x, int y)
	{
		if (Itemstems == null) return false;
		if (x < 0 || x >= GridShape.GridWidth || y < 0 || y >= GridShape.GridHeight) return false;
		if (!GridShape.GetGridShapeCell(x, y)) return false; // Check inventory shape validity

		return Itemstems[x, y] != null; // Check if cell is occupied
	}

	public bool HasItemAt(int x, int y, Item item)
	{
		if (!HasItemAt(x, y)) return false;

		if (Itemstems[x, y] == item) return true;
		else return false;
	}

	private Item GetItemAt(int x, int y)
	{
		if (HasItemAt(x, y))
		{
			return Itemstems[x, y];
		}

		return null;
	}

	public bool TryGetItemAt(int x, int y, out Item item)
	{
		item = null;
		if (!HasItemAt(x, y)) return false;
		item = GetItemAt(x, y);
		return true;
	}

	public List<Vector2I> GetItemPositions(Item item)
	{
		List<Vector2I> result = new List<Vector2I>();
		if (item == null || Itemstems == null) return result;

		for (int x = 0; x < GridShape.GridWidth; x++)
		{
			for (int y = 0; y < GridShape.GridHeight; y++)
			{
				if (Itemstems[x, y] == item)
				{
					Vector2I pos = new Vector2I(x, y);
					if (!result.Contains(pos))
					{
						result.Add(pos);
					}
				}
			}
		}

		return result;
	}

/// <summary>
/// Attempts to transfer an item from one inventory grid to another.
/// Performs all validation checks upfront.
/// </summary>
/// <param name="sourceInventory">The inventory grid to remove the item from.</param>
/// <param name="destinationInventory">The inventory grid to add the item to.</param>
/// <param name="item">The item to transfer. Must not be null.</param>
/// <returns>True if the item was successfully transferred; otherwise, false.</returns>
public static bool TryTransferItem(InventoryGrid sourceInventory, InventoryGrid destinationInventory, Item item)
{
	if (sourceInventory == null || destinationInventory == null || item == null)
	{
		GD.PrintErr("TryTransferItem: One or more parameters are null.");
		return false;
	}

	if (!sourceInventory.HasItem(item))
	{
		GD.Print($"TryTransferItem: Item '{item.ItemData?.ItemName ?? "Unknown"}' not found in source inventory.");
		return false;
	}

	if (!destinationInventory.CanAddItem(item, out _))
	{
		GD.Print($"TryTransferItem: Cannot add item '{item.ItemData?.ItemName ?? "Unknown"}' to destination inventory.");
		return false;
	}

	sourceInventory.RemoveItem(item);
	destinationInventory.AddItem(item);
	GD.Print($"TryTransferItem: Successfully transferred item '{item.ItemData?.ItemName ?? "Unknown"}'.");
	return true;
}

/// <summary>
/// Attempts to transfer the item located at a specific position in the source inventory
/// to a specific position in the destination inventory.
/// Performs all validation checks upfront.
/// </summary>
/// <param name="sourceInventory">The inventory grid to remove the item from.</param>
/// <param name="sourcePosition">The grid coordinates (x, y) of the item in the source inventory.</param>
/// <param name="destinationInventory">The inventory grid to add the item to.</param>
/// <param name="destinationPosition">The grid coordinates (x, y) in the destination inventory where the item should be placed.</param>
/// <param name="item">Outputs the item that was attempted to be transferred. Null if no item was found at the source position.</param>
/// <returns>True if the item was successfully transferred; otherwise, false.</returns>
public static bool TryTransferItemAt(InventoryGrid sourceInventory, Vector2I sourcePosition,
	InventoryGrid destinationInventory, Vector2I destinationPosition, out Item item)
{
	item = null;

	if (sourceInventory == null || destinationInventory == null)
	{
		GD.PrintErr("TryTransferItemAt: One or more inventory parameters are null.");
		return false;
	}

	if (!sourceInventory.TryGetItemAt(sourcePosition.X, sourcePosition.Y, out item) || item == null)
	{
		GD.Print($"TryTransferItemAt: No item found at source position ({sourcePosition.X}, {sourcePosition.Y}).");
		return false;
	}

	if (!destinationInventory.CanAddItemAt(destinationPosition.X, destinationPosition.Y, item))
	{
		GD.Print($"TryTransferItemAt: Cannot add item '{item.ItemData?.ItemName ?? "Unknown"}' to destination position ({destinationPosition.X}, {destinationPosition.Y}).");
		item = null; // Reset item output as the transfer failed
		return false;
	}

	sourceInventory.RemoveItem(item);
	destinationInventory.AddItemAt(destinationPosition, item);
	GD.Print($"TryTransferItemAt: Successfully transferred item '{item.ItemData?.ItemName ?? "Unknown"}' from ({sourcePosition.X}, {sourcePosition.Y}) to ({destinationPosition.X}, {destinationPosition.Y}).");
	return true;
}

/// <summary>
/// Attempts to transfer the item located at a specific position in the source inventory
/// to a specific position in the destination inventory. (Overload without returning the item reference).
/// </summary>
/// <param name="sourceInventory">The inventory grid to remove the item from.</param>
/// <param name="sourcePosition">The grid coordinates (x, y) of the item in the source inventory.</param>
/// <param name="destinationInventory">The inventory grid to add the item to.</param>
/// <param name="destinationPosition">The grid coordinates (x, y) in the destination inventory where the item should be placed.</param>
/// <returns>True if the item was successfully transferred; otherwise, false.</returns>
public static bool TryTransferItemAt(InventoryGrid sourceInventory, Vector2I sourcePosition,
	InventoryGrid destinationInventory, Vector2I destinationPosition)
{
	return TryTransferItemAt(sourceInventory, sourcePosition, destinationInventory, destinationPosition, out _);
}
	#endregion
}