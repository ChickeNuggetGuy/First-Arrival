using System.Collections.Generic;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Inventory_System;

[GlobalClass]
public partial class InventoryGrid : Resource
{
	[Export] public Enums.InventoryType InventoryType { get; protected set; }

	[Export] public GridShape GridShape { get; protected set; }
	[Export(PropertyHint.Enum)] public Enums.InventorySettings InventorySettings { get; protected set; }
	public Item[,] Items{get; private set;}

	/// <summary>
	/// Gets the nummber of unique Items in the grid
	/// </summary>
	public int ItemCount
	{
		get
		{
			if (Items == null) return -1;
			List<Item> uniqueItems = new  List<Item>();

			for (int x = 0; x < Items.GetLength(0); x++)
			{
				for (int y = 0; y < Items.GetLength(1); y++)
				{
					if  (Items[x, y] == null) continue;
					if (uniqueItems.Contains(Items[x, y])) continue;
					uniqueItems.Add(Items[x, y]);
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

			for (int x = 0; x < Items.GetLength(0); x++)
			{
				for (int y = 0; y < Items.GetLength(1); y++)
				{
					if  (Items[x, y] == null) continue;
					if (uniqueItems.Contains(Items[x, y])) continue;
					uniqueItems.Add(Items[x, y]);
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
		Items = new Item[GridShape.GridWidth, GridShape.GridHeight];
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
		if (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
		{
			// An item can occupy multiple cells based on its ItemShape.
			GridShape itemShape = item?.ItemData?.ItemShape;
			
			if (itemShape == null)
			{
				// Fallback or error handling. Placing in single cell as a default.
				GD.PushWarning($"AddItemAt: Item '{item?.ItemData?.ItemName ?? "Unknown"}' has no ItemShape but UseItemSize is true. Placing in single cell ({x},{y}).");
				if (x >= 0 && x < GridShape.GridWidth && y >= 0 && y < GridShape.GridHeight && GridShape.GetGridShapeCell(x, y))
				{
					Items[x, y] = item;
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
							Items[gridX, gridY] = item;
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
				Items[x, y] = item;
			}
			// Optionally, else log error if CanAddItemAt was supposed to prevent this.
			// else { GD.PrintErr($"AddItemAt: Position ({x},{y}) is out of bounds or invalid for inventory shape (UseItemSize=false)."); }
		}
		// --- End of Correction ---
		item.currentGrid = this;
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
			Items[pos.X, pos.Y] = null;
		}
		item.currentGrid = null;
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
		for (int x = 0; x <= GridShape.GridWidth - (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null ? itemShape.GridWidth : 1); x++)
		{
			for (int y = 0; y <= GridShape.GridHeight - (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null ? itemShape.GridHeight : 1); y++)
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
		
		if (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
		{
			GridShape itemShape = item?.ItemData?.ItemShape;
			
			if (itemShape == null)
			{
				GD.PushWarning($"CanAddItemAt: Item '{item?.ItemData?.ItemName ?? "Unknown"}' has no ItemShape but UseItemSize is true. Cannot place.");
				return false;
			}
			
			for (int relX = 0; relX < itemShape.GridWidth; relX++)
			{
				for (int relY = 0; relY < itemShape.GridHeight; relY++)
				{
					if (!itemShape.GetGridShapeCell(relX, relY))
					{
						continue; // This part of the item's bounding box is empty, skip it.
					}

					int gridX = x + relX;
					int gridY = y + relY;
					
					if (gridX < 0 || gridX >= GridShape.GridWidth || gridY < 0 || gridY >= GridShape.GridHeight)
					{
						return false; // Part of the item would be placed outside the inventory grid.
					}
					
					if (!GridShape.GetGridShapeCell(gridX, gridY))
					{
						return false; // This cell in the inventory is not part of the usable space.
					}
					
					if (Items[gridX, gridY] != null)
					{
						return false; // Cell is already occupied by another item.
					}

				}
			}
		}
		else
		{
			if (x < 0 || x >= GridShape.GridWidth || y < 0 || y >= GridShape.GridHeight)
			{
				return false; // Position is outside the grid dimensions.
			}
			
			if (!GridShape.GetGridShapeCell(x, y))
			{
				return false; // This cell is not part of the usable inventory space.
			}


			if (Items[x, y] != null)
			{
				return false; // Cell is already occupied.
			}
		}


		return true;
	}
	

	public bool HasItem(Item item)
	{
		if (item == null) return false;
		if (Items == null) return false;

		for (int x = 0; x < GridShape.GridWidth; x++)
		{
			for (int y = 0; y < GridShape.GridHeight; y++)
			{
				if (Items[x, y] == item) return true;
			}
		}

		return false;
	}

	public bool HasItemAt(int x, int y)
	{
		if (Items == null) return false;
		if (x < 0 || x >= GridShape.GridWidth || y < 0 || y >= GridShape.GridHeight) return false;
		if (!GridShape.GetGridShapeCell(x, y)) return false; // Check inventory shape validity

		return Items[x, y] != null; // Check if cell is occupied
	}

	public bool HasItemAt(int x, int y, Item item)
	{
		if (!HasItemAt(x, y)) return false;

		if (Items[x, y] == item) return true;
		else return false;
	}

	private Item GetItemAt(int x, int y)
	{
		if (HasItemAt(x, y))
		{
			return Items[x, y];
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
		if (item == null || Items == null) return result;

		for (int x = 0; x < GridShape.GridWidth; x++)
		{
			for (int y = 0; y < GridShape.GridHeight; y++)
			{
				if (Items[x, y] == item)
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