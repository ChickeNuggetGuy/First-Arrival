using System.Collections.Generic;
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
        protected set
        {
            _inventorySettings = value;
            NotifyPropertyListChanged();
        }
    }

    public int maxItemCount = 0;
    public int maxWeight { get; protected set; }
    public (Item item, int count)[,] Items { get; private set; }

    // Caching for performance
    private List<(Item item, int count)> _uniqueItemsCache;
    private bool _isCacheDirty = true;

    /// <summary>
    /// Gets the number of unique Items in the grid
    /// </summary>
    public int ItemCount
    {
        get
        {
            if (Items == null) return -1;
            return UniqueItems.Count;
        }
    }

    public int ItemWeight
    {
        get
        {
            int totalWeight = 0;
            if (Items == null) return -1;

            foreach (var i in UniqueItems)
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
            if (_isCacheDirty || _uniqueItemsCache == null)
            {
                _uniqueItemsCache = new List<(Item item, int count)>();

                for (int x = 0; x < Items.GetLength(0); x++)
                {
                    for (int y = 0; y < Items.GetLength(1); y++)
                    {
                        if (Items[x, y].item == null) continue;
                        if (_uniqueItemsCache.Contains(Items[x, y])) continue;
                        _uniqueItemsCache.Add(Items[x, y]);
                    }
                }

                _isCacheDirty = false;
            }

            return _uniqueItemsCache;
        }
    }

    #region Signals

    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void ItemAddedEventHandler(Item itemAdded);

    [Signal]
    public delegate void ItemRemovedEventHandler(Item itemRemoved);

    #endregion

    #region Initialization & Grid Utilities

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        var properties = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        if (InventorySettings.HasFlag(Enums.InventorySettings.MaxItemAmount))
        {
            properties.Add(new Dictionary()
            {
                { "name", "maxItemCount" },
                { "type", (int)Variant.Type.Int },
                { "hint_string", "Item Count" },
            });
        }

        if (InventorySettings.HasFlag(Enums.InventorySettings.MaxWeight))
        {
            properties.Add(new Dictionary()
            {
                { "name", "maxWeight" },
                { "type", (int)Variant.Type.Int },
                { "hint_string", "1,2,3,4,5,6,7,8,9" },
            });
        }

        return properties;
    }

    public void Initialize()
    {
        if (GridShape == null)
        {
            GD.PushError("Initialize called before GridShape was assigned.");
            return;
        }

        Items = new (Item item, int count)[GridShape.GridSizeX, GridShape.GridSizeZ];

        for (int x = 0; x < GridShape.GridSizeX; x++)
        {
            for (int y = 0; y < GridShape.GridSizeZ; y++)
            {
                Items[x, y] = (null, 0);
            }
        }

        _isCacheDirty = true;
    }

    private bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < GridShape.GridSizeX &&
               y >= 0 && y < GridShape.GridSizeZ &&
               (!_inventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) || GridShape.GetGridShapeCell(x, 1, y));
    }

    private bool IsValidForPlacement(int x, int y)
    {
        // Basic rectangular boundary check
        if (x < 0 || x >= GridShape.GridSizeX || y < 0 || y >= GridShape.GridSizeZ)
            return false;

        // If no shape logic enabled — any valid coordinate is usable
        if (!_inventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
            return true;

        // Respect actual shape layout when using item shapes
        return GridShape.GetGridShapeCell(x, 1, y);
    }

    #endregion

    #region Add / Remove / Check Items

    private void AddItem(Item item, int count)
    {
        if (item == null) return;

        if (!CanAddItem(item, out var position, count, out string reason))
        {
            GD.Print($"Error: can not add item to Grid: {reason}");
            return;
        }

        AddItemAt(position, item, count);
    }

    private void AddItemAt(Vector2I position, Item item, int count)
    {
        AddItemAt(position.X, position.Y, item, count);
    }

    private void AddItemAt(int x, int y, Item item, int count)
    {
        if (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
        {
            GridShape itemShape = item?.ItemData?.ItemShape;

            if (itemShape == null)
            {
                GD.PushWarning($"AddItemAt: Item '{item?.ItemData?.ItemName ?? "Unknown"}' has no ItemShape. Placing in single cell.");

                if (IsValidCell(x, y))
                {
                    Items[x, y] = (item, count);
                }
            }
            else
            {
                for (int relX = 0; relX < itemShape.GridSizeX; relX++)
                {
                    for (int relY = 0; relY < itemShape.GridSizeZ; relY++)
                    {
                        if (!itemShape.GetGridShapeCell(relX, 1, relY)) continue;

                        int gridX = x + relX;
                        int gridY = y + relY;

                        if (IsValidCell(gridX, gridY))
                        {
                            Items[gridX, gridY] = (item, count);
                        }
                    }
                }
            }
        }
        else
        {
            if (IsValidCell(x, y))
            {
                Items[x, y] = (item, count);
            }
        }

        item.currentGrid = this;
        EmitSignal(SignalName.ItemAdded, item);
        EmitSignal(SignalName.InventoryChanged);

        _isCacheDirty = true;
    }

    public bool TryAddItem(Item item, int count)
    {
        if (!CanAddItem(item, out Vector2I position, count, out string reason))
        {
            GD.Print($"Cannot add item to Grid: {reason}");
            return false;
        }

        AddItemAt(position.X, position.Y, item, count);
        return true;
    }

    public bool TryAddItemAt(Item item, Vector2I position, int count)
    {
        if (CanAddItemAt(position.X, position.Y, item, count, out string reason))
        {
            AddItemAt(position.X, position.Y, item, count);
            return true;
        }

        GD.Print(reason);
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

        _isCacheDirty = true;
    }

    public bool TryRemoveItem(Item item, int count)
    {
        if (!HasItem(item)) return false;
        RemoveItem(item, count);
        return true;
    }

    public bool HasItem(Item item)
    {
        if (item == null || Items == null) return false;

        for (int x = 0; x < GridShape.GridSizeX; x++)
        {
            for (int y = 0; y < GridShape.GridSizeZ; y++)
            {
                if (Items[x, y].item?.ItemData == item.ItemData) return true;
            }
        }

        return false;
    }

    public bool HasItemAt(int x, int y)
    {
        if (Items == null || !IsValidCell(x, y)) return false;
        return Items[x, y].item != null;
    }

    public bool HasItemAt(int x, int y, Item item)
    {
        return HasItemAt(x, y) && Items[x, y].item?.ItemData == item.ItemData;
    }

    private (Item item, int count) GetItemAt(int x, int y)
    {
        return HasItemAt(x, y) ? Items[x, y] : (null, 0);
    }

    public bool TryGetItemAt(int x, int y, out (Item item, int count) item)
    {
        item = (null, 0);
        if (!HasItemAt(x, y)) return false;

        item = GetItemAt(x, y);
        return true;
    }

    public List<Vector2I> GetItemPositions(Item item)
    {
        List<Vector2I> result = new();

        if (item == null || Items == null) return result;

        for (int x = 0; x < GridShape.GridSizeX; x++)
        {
            for (int y = 0; y < GridShape.GridSizeZ; y++)
            {
                if (Items[x, y].item?.ItemData == item.ItemData)
                {
                    Vector2I pos = new(x, y);
                    if (!result.Contains(pos))
                    {
                        result.Add(pos);
                    }
                }
            }
        }

        return result;
    }

    #endregion

    #region CanAdd Validation Logic

    public bool CanAddItem(Item item, out Vector2I position, int count, out string reason)
    {
        reason = "N/A";
        position = new Vector2I(-1, -1);

        if (item == null)
        {
            reason = "Item is null";
            return false;
        }

        GridShape itemShape = item.ItemData?.ItemShape;

        int width = (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null) ? itemShape.GridSizeX : 1;
        int height = (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null) ? itemShape.GridSizeZ : 1;

        for (int x = 0; x <= GridShape.GridSizeX - width; x++)
        {
            for (int y = 0; y <= GridShape.GridSizeZ - height; y++)
            {
                if (CanAddItemAt(x, y, item, count, out reason))
                {
                    position = new Vector2I(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    public bool CanAddItemAt(int x, int y, Item item, int count, out string reason)
    {
        reason = "";

        if (item == null)
        {
            reason = "Item is null.";
            return false;
        }

        if (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
        {
            GridShape itemShape = item.ItemData?.ItemShape;

            if (itemShape == null)
            {
                reason = $"Item '{item.ItemData?.ItemName}' has no ItemShape but UseItemShapes is on.";
                return false;
            }

            for (int relX = 0; relX < itemShape.GridSizeX; relX++)
            {
                for (int relY = 0; relY < itemShape.GridSizeZ; relY++)
                {
                    if (!itemShape.GetGridShapeCell(relX, 1, relY)) continue;

                    int gridX = x + relX;
                    int gridY = y + relY;

                    if (!IsValidCell(gridX, gridY))
                    {
                        reason = $"Position ({gridX}, {gridY}) is invalid or outside bounds.";
                        return false;
                    }

                    if (Items[gridX, gridY].item != null)
                    {
                        if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                        {
                            if (Items[gridX, gridY].item.ItemData == item.ItemData &&
                                Items[gridX, gridY].count + count <= item.ItemData.MaxStackSize)
                                continue;
                            else
                            {
                                reason = "Invalid stacking attempt – mismatch or stack overflow.";
                                return false;
                            }
                        }
                        else
                        {
                            reason = "Cell already occupied and stacking disabled.";
                            return false;
                        }
                    }
                }
            }
        }
        else
        {
            // Rectangular inventory — straightforward allocation without shape
            if (!IsValidForPlacement(x, y))
            {
                reason = "Cell is outside boundaries or not part of inventory shape.";
                return false;
            }

            if (Items[x, y].item != null)
            {
                if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                {
                    if (Items[x, y].item.ItemData == item.ItemData &&
                        Items[x, y].count + count <= item.ItemData.MaxStackSize)
                        return true;
                    else
                    {
                        reason = "Cannot stack – full or non-stackable item.";
                        return false;
                    }
                }
                else
                {
                    reason = "Slot already occupied and stacking disabled.";
                    return false;
                }
            }
        }

        return true;
    }

    #endregion

    #region Static Transfer Helpers

    public static bool TryTransferItem(InventoryGrid source, InventoryGrid destination, Item item, int count)
    {
        if (source == null || destination == null || item == null)
        {
            GD.PrintErr("TryTransferItem: One or more parameters are null.");
            return false;
        }

        if (!source.HasItem(item))
        {
            GD.Print($"Item '{item.ItemData?.ItemName}' not found in source.");
            return false;
        }

        if (!destination.CanAddItem(item, out _, count, out var reason))
        {
            GD.Print($"Cannot add item to destination: {reason}");
            return false;
        }

        source.RemoveItem(item, count);
        destination.AddItem(item, count);
        GD.Print($"Transferred item '{item.ItemData?.ItemName}' x{count} successfully.");
        return true;
    }

    public static bool TryTransferItemAt(
        InventoryGrid source, Vector2I srcPos,
        InventoryGrid dest, Vector2I dstPos, out Item item)
    {
        item = null;

        if (source == null || dest == null ||
            !source.TryGetItemAt(srcPos.X, srcPos.Y, out var data) || data.item == null)
        {
            GD.PrintErr("Failed to retrieve item in source inventory.");
            return false;
        }

        item = data.item;

        if (!dest.CanAddItemAt(dstPos.X, dstPos.Y, item, data.count, out var reason))
        {
            GD.Print($"Transfer blocked by destination: {reason}");
            return false;
        }

        source.RemoveItem(data.item, data.count);
        dest.AddItemAt(dstPos, data.item, data.count);

        GD.Print("Transfer successful!");
        return true;
    }

    public static bool TryTransferItemAt(
        InventoryGrid source, Vector2I srcPos,
        InventoryGrid dest, Vector2I dstPos)
    {
        return TryTransferItemAt(source, srcPos, dest, dstPos, out _);
    }

    #endregion
}