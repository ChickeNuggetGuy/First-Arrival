using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Inventory_System;

[GlobalClass, Tool]
public partial class InventoryGrid : Resource
{
	[Export] public Enums.InventoryType InventoryType { get; protected set; }

	[Export(PropertyHint.ResourceType, "GridShape")] public GridShape GridShape { get; protected set; }

	private Enums.InventorySettings _inventorySettings;
	[Export(PropertyHint.Enum)]
	public Enums.InventorySettings InventorySettings
	{
		get => _inventorySettings;
		protected set{			
			_inventorySettings = value;
			NotifyPropertyListChanged();}
	}
	
	public int maxItemCount = 0;
	public int maxWeight { get; protected set; }
	public (Item item, int count)[,] Items{get; private set;}

	/// <summary>
	/// Gets the nummber of unique Items in the grid
	/// </summary>
	public int ItemCount
	{
		get
		{
			int count = 0;
			if (Items == null) return -1;
			List<Item> uniqueItems = new  List<Item>();

			for (int x = 0; x < Items.GetLength(0); x++)
			{
				for (int y = 0; y < Items.GetLength(1); y++)
				{
					if  (Items[x, y].item == null) continue;
					if (uniqueItems.Contains(Items[x, y].item)) continue;
					if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
					{
						count += Items[x, y].count;
					}
					else
					{
						count++;
					}
					uniqueItems.Add(Items[x, y].item);
				}
			}
			return uniqueItems.Count;
		}
	}
	
	public int ItemWeight
	{
		get
		{
			int totalWeight = 0;
			if (Items == null) return -1;
			List<(Item item, int count)> uniqueItems = UniqueItems;

			foreach ((Item item, int count) i in uniqueItems)
			{
				totalWeight += i.item.ItemData.weight * i.count;
			}
			return totalWeight;
		}
	}
	public List<(Item item, int count)> UniqueItems
	{
		get
		{
			List<(Item item, int count)> uniqueItems = new  List<(Item item, int count)>();

			for (int x = 0; x < Items.GetLength(0); x++)
			{
				for (int y = 0; y < Items.GetLength(1); y++)
				{
					if  (Items[x, y].item == null) continue;
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

	public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
	{
		Godot.Collections.Array<Godot.Collections.Dictionary> properties = [];

		if (InventorySettings.HasFlag(Enums.InventorySettings.MaxItemAmount))
		{
			properties.Add(new Godot.Collections.Dictionary()
			{
				{ "name", $"maxItemCount" },
				{ "type", (int)Variant.Type.Int },
				{ "hint_string", "Item Count" },
			});
		}
		
		if (InventorySettings.HasFlag(Enums.InventorySettings.MaxWeight))
		{
			properties.Add(new Godot.Collections.Dictionary()
			{
				{ "name", $"maxWeight" },
				{ "type", (int)Variant.Type.Int },
				{ "hint_string", "1,2,3,4,5,6,7,8,9" },
			});
		}

		return properties;
	}
	
	public void Initialize()
	{
		Items = new (Item item, int count)[GridShape.GridSizeX, GridShape.GridSizeZ];

		for (int x = 0; x < GridShape.GridSizeX; x++)
		{
			for (int y = 0; y < GridShape.GridSizeZ; y++)
			{
				Items[x, y] = (null, 0);
			}
		}
	}

	private void AddItem(Item item, int count)
	{
		if (item == null) return;

		if (!CanAddItem(item, out var position,count)) return;
		AddItemAt(position, item, count);
	}
	
	private void AddItemAt(Vector2I position, Item item, int count) => AddItemAt(position.X, position.Y, item,count);

	private void AddItemAt(int x, int y, Item item, int count)
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
				if (x >= 0 && x < GridShape.GridSizeX && y >= 0 && y < GridShape.GridSizeZ && GridShape.GetGridShapeCell(x, y))
				{
					Items[x, y] = (item, count);
				}
			}
			else
			{
				// Place the item in all cells defined by its ItemShape, offset by (x, y).
				for (int relX = 0; relX < itemShape.GridSizeX; relX++)
				{
					for (int relY = 0; relY < itemShape.GridSizeZ; relY++)
					{
						// Check if this part of the item's shape is  occupied/used.
						if (!itemShape.GetGridShapeCell(relX, relY))
						{
							continue;
						}

						int gridX = x + relX;
						int gridY = y + relY;

						// Bounds check (should ideally be done in CanAddItemAt, but safe here as a safeguard)
						// Also check if the calculated grid cell is part of the inventory's shape.
						if (gridX >= 0 && gridX < GridShape.GridSizeX && gridY >= 0 && gridY < GridShape.GridSizeZ && GridShape.GetGridShapeCell(gridX, gridY))
						{
							Items[gridX, gridY] = (item, count);
						}
					}
				}
			}
		}
		else
		{
			// Bounds and shape check (should be ensured by CanAddItemAt, but safe here as a safeguard)
			if (x >= 0 && x < GridShape.GridSizeX && y >= 0 && y < GridShape.GridSizeZ && GridShape.GetGridShapeCell(x, y))
			{
				Items[x, y] = (item, count);;
			}
		}
		// --- End of Correction ---
		item.currentGrid = this;
		EmitSignal(SignalName.ItemAdded, item);
		EmitSignal(SignalName.InventoryChanged);
	}

	public bool TryAddItem(Item item, int count)
	{
		if (!CanAddItem(item, out Vector2I position, count))
			return false;

		AddItemAt(position.X, position.Y, item, count);
		return true;
	}

	public bool TryAddItemAt(Item item, Vector2I position, int count)
	{

		if (CanAddItemAt(position.X, position.Y, item, count))
		{
			AddItemAt(position.X, position.Y, item, count);
			return true; // Indicate successful addition.
		}

		return false; 
	}
	
	private void RemoveItem(Item item, int count)
	{
		if (item == null) return;
		List<Vector2I> positions = GetItemPositions(item);
		foreach (Vector2I pos in positions)
		{
			if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking) && Items[pos.X, pos.Y].count >= count)
			{
				Items[pos.X, pos.Y].count -= count;
			}
			else
			{
				Items[pos.X, pos.Y] = (null, 0);
			}
		}
		item.currentGrid = null;
		EmitSignal(SignalName.ItemRemoved, item);
		EmitSignal(SignalName.InventoryChanged);
	}

	public bool TryRemoveItem(Item item, int count)
	{
		if (!HasItem(item)) return false;
		RemoveItem(item, count);
		return true;
	}

	public bool CanAddItem(Item item, out Vector2I position, int count)
	{
		position = new Vector2I(-1, -1);
		if (item == null) return false;

		GridShape itemShape = item.ItemData?.ItemShape;


		for (int x = 0; x <= GridShape.GridSizeX - (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null ? itemShape.GridSizeX : 1); x++)
		{
			for (int y = 0; y <= GridShape.GridSizeZ - (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null ? itemShape.GridSizeZ : 1); y++)
			{

				if (CanAddItemAt(x, y, item, count)) 
				{
					position = new Vector2I(x, y);
					return true;
				}
			}
		}

		return false;
	}

	public bool CanAddItemAt(int x, int y, Item item, int count)
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
			
			for (int relX = 0; relX < itemShape.GridSizeX; relX++)
			{
				for (int relY = 0; relY < itemShape.GridSizeZ; relY++)
				{
					if (!itemShape.GetGridShapeCell(relX, relY))
					{
						continue; // This part of the item's bounding box is empty, skip it.
					}

					int gridX = x + relX;
					int gridY = y + relY;
					
					if (gridX < 0 || gridX >= GridShape.GridSizeX || gridY < 0 || gridY >= GridShape.GridSizeZ)
					{
						return false; // Part of the item would be placed outside the inventory grid.
					}
					
					if (!GridShape.GetGridShapeCell(gridX, gridY))
					{
						return false; // This cell in the inventory is not part of the usable space.
					}

		
					if (Items[gridX, gridY].item != null)
					{
						if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
						{
							if (Items[gridX,gridY].item.ItemData == item.ItemData && Items[gridX, gridY].count < item.ItemData.MaxStackSize)
							{
								return true;
							}
							else
							{
								return false;
							}
						}
						else
						{
							return false;
						}
					}

				}
			}
		}
		else
		{
			if (x < 0 || x >= GridShape.GridSizeX || y < 0 || y >= GridShape.GridSizeZ)
			{
				return false; // Position is outside the grid dimensions.
			}
			
			if (!GridShape.GetGridShapeCell(x, y))
			{
				return false; // This cell is not part of the usable inventory space.
			}


			if (Items[x, y].item != null)
			{
				if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
				{
					if (Items[x, y].item.ItemData == item.ItemData && Items[x, y].count < item.ItemData.MaxStackSize)
					{
						return true;
					}
					else
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}
		}
		return true;
	}
	

	public bool HasItem(Item item)
	{
		if (item == null) return false;
		if (Items == null) return false;

		for (int x = 0; x < GridShape.GridSizeX; x++)
		{
			for (int y = 0; y < GridShape.GridSizeZ; y++)
			{
				if (Items[x, y].item.ItemData == item.ItemData) return true;
			}
		}

		return false;
	}

	public bool HasItemAt(int x, int y)
	{
		if (Items == null) return false;
		if (x < 0 || x >= GridShape.GridSizeX || y < 0 || y >= GridShape.GridSizeZ) return false;
		if (!GridShape.GetGridShapeCell(x, y)) return false; // Check inventory shape validity

		return Items[x, y].item != null; // Check if cell is occupied
	}

	public bool HasItemAt(int x, int y, Item item)
	{
		if (!HasItemAt(x, y)) return false;

		if (Items[x, y].item.ItemData == item.ItemData) return true;
		else return false;
	}

	private (Item item, int count) GetItemAt(int x, int y)
	{
		if (HasItemAt(x, y))
		{
			return Items[x, y];
		}

		return (null,0);
	}

	public bool TryGetItemAt(int x, int y, out (Item item, int count) item)
	{
		item = (null,0);
		if (!HasItemAt(x, y)) return false;
		item = GetItemAt(x, y);
		return true;
	}

	public List<Vector2I> GetItemPositions(Item item)
	{
		List<Vector2I> result = new List<Vector2I>();
		if (item == null || Items == null) return result;

		for (int x = 0; x < GridShape.GridSizeX; x++)
		{
			for (int y = 0; y < GridShape.GridSizeZ; y++)
			{
				if (Items[x, y].item.ItemData == item.ItemData)
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
	/// <param name="count">The amount of said item to transfer</param>
	/// <returns>True if the item was successfully transferred; otherwise, false.</returns>
	public static bool TryTransferItem(InventoryGrid sourceInventory, InventoryGrid destinationInventory, Item item, int count)
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

	if (!destinationInventory.CanAddItem(item, out _, count))
	{
		GD.Print($"TryTransferItem: Cannot add item '{item.ItemData?.ItemName ?? "Unknown"}' to destination inventory.");
		return false;
	}

	sourceInventory.RemoveItem(item, count);
	destinationInventory.AddItem(item, count);
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

	if (!sourceInventory.TryGetItemAt(sourcePosition.X, sourcePosition.Y, out var itemInfo) || itemInfo.item == null)
	{
		GD.Print($"TryTransferItemAt: No item found at source position ({sourcePosition.X}, {sourcePosition.Y}).");
		return false;
	}

	item = itemInfo.item;

	if (!destinationInventory.CanAddItemAt(destinationPosition.X, destinationPosition.Y, item, itemInfo.count))
	{
		GD.Print($"TryTransferItemAt: Cannot add item '{item.ItemData?.ItemName ?? "Unknown"}' to destination position ({destinationPosition.X}, {destinationPosition.Y}).");
		item = null; // Reset item output as the transfer failed
		return false;
	}

	sourceInventory.RemoveItem(itemInfo.item, itemInfo.count);
	destinationInventory.AddItemAt(destinationPosition, item, itemInfo.count);
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