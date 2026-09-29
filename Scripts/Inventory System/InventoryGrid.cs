using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Inventory_System;

[GlobalClass, Tool]
public partial class InventoryGrid : Resource
{
    public GridObject OwningUnit { get; set; }
    public GridCell GroundCell { get; set; }
    [Export] public Enums.InventoryType InventoryType { get; protected set; }

    [Export(PropertyHint.ResourceType, "GridShape")] public GridShape GridShape { get; protected set; }

    private Enums.InventorySettings _inventorySettings;
    [Export]
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
    private readonly List<(Item item, int count)[,]> _pages = new();
    private int _currentPageIndex;

    /// <summary>
    /// The cells on the page currently shown by an InventoryGridUI.
    /// Single-page inventories always expose page zero.
    /// </summary>
    public (Item item, int count)[,] Items =>
        _pages.Count == 0 ? null : _pages[_currentPageIndex];

    public int CurrentPageIndex => _currentPageIndex;
    public int PageCount => _pages.Count;
    public bool AllowsMultiplePages =>
        InventorySettings.HasFlag(Enums.InventorySettings.AllowMultiplePages);

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

                foreach (var page in _pages)
                {
                    for (int x = 0; x < page.GetLength(0); x++)
                    {
                        for (int y = 0; y < page.GetLength(1); y++)
                        {
                            if (page[x, y].item == null) continue;
                            if (_uniqueItemsCache.Contains(page[x, y])) continue;
                            _uniqueItemsCache.Add(page[x, y]);
                        }
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
    public delegate void ItemAddedEventHandler(InventoryGrid inventoryGrid, Item itemAdded);

    [Signal]
    public delegate void ItemRemovedEventHandler(InventoryGrid inventoryGrid,Item itemRemoved);

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

        _pages.Clear();
        _pages.Add(CreateEmptyPage());
        _currentPageIndex = 0;

        _isCacheDirty = true;
    }

    private (Item item, int count)[,] CreateEmptyPage()
    {
        return new (Item item, int count)[GridShape.SizeX, GridShape.SizeZ];
    }

    /// <summary>
    /// Changes which page coordinate-based operations and the UI refer to.
    /// </summary>
    public bool SetCurrentPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _pages.Count || pageIndex == _currentPageIndex)
            return false;

        _currentPageIndex = pageIndex;
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool TryGetPageItems(int pageIndex, out (Item item, int count)[,] pageItems)
    {
        pageItems = null;
        if (pageIndex < 0 || pageIndex >= _pages.Count) return false;

        pageItems = _pages[pageIndex];
        return true;
    }

    private bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < GridShape.SizeX &&
               y >= 0 && y < GridShape.SizeZ &&
               (!_inventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) || GridShape.IsOccupied(x, 0, y));
    }

    private bool IsValidForPlacement(int x, int y)
    {
        // Basic rectangular boundary check
        if (x < 0 || x >= GridShape.SizeX || y < 0 || y >= GridShape.SizeZ)
            return false;

        // If no shape logic enabled — any valid coordinate is usable
        if (!_inventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
            return true;

        // Respect actual shape layout when using item shapes
        return GridShape.IsOccupied(x, 0, y);
    }

    #endregion

    #region Add / Remove / Check Items

    private bool AddItem(Item item, int count)
    {
        if (item == null) return false;

        if (!TryFindItemPlacement(item, count, out int pageIndex, out var position, out string reason))
        {
            GD.Print($"Error: can not add item to Grid: {reason}");
            return false;
        }

        if (pageIndex == _pages.Count)
            _pages.Add(CreateEmptyPage());

        AddItemAt(position.X, position.Y, item, count, pageIndex);
        return true;
    }

    private void AddItemAt(Vector2I position, Item item, int count)
    {
        AddItemAt(position.X, position.Y, item, count, _currentPageIndex);
    }

    private void AddItemAt(int x, int y, Item item, int count, int pageIndex)
    {
        var pageItems = _pages[pageIndex];
        _isCacheDirty = true;

        if (InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes))
        {
            GridShape itemShape = item?.ItemData?.ItemShape;

            if (itemShape == null)
            {
                GD.PushWarning($"AddItemAt: Item '{item?.ItemData?.ItemName ?? "Unknown"}' has no ItemShape. Placing in single cell.");

                if (IsValidCell(x, y))
                {
                     // Logic for merging/overwriting single cell
                     if(pageItems[x, y].item != null && pageItems[x, y].item.ItemData == item.ItemData && _inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                     {
                         // Merging into existing single-cell stack
                         pageItems[x, y].count += count;
                     }
                     else
                     {
                         pageItems[x, y] = (item, count);
                         item.currentGrid = this;
                         EmitSignal(SignalName.ItemAdded, this, item);
                     }
                }
            }
            else
            {
                // Check for merging FIRST. 
                // We must check ALL cells the new item would occupy. If ANY contain a compatible item, we merge into THAT item.
                Item existingItem = null;
                
                // Scan the shape to find an existing item to merge into
                for (int relX = 0; relX < itemShape.SizeX; relX++)
                {
                    for (int relY = 0; relY < itemShape.SizeZ; relY++)
                    {
                        if (!itemShape.IsOccupied(relX, 0, relY)) continue;
                        int gridX = x + relX;
                        int gridY = y + relY;
                        
                        if (IsValidCell(gridX, gridY) && pageItems[gridX, gridY].item != null)
                        {
                            // Found an item. Check if compatible.
                            if (pageItems[gridX, gridY].item.ItemData.ItemID == item.ItemData.ItemID &&
                                _inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                            {
                                existingItem = pageItems[gridX, gridY].item;
                                break; // Found our target
                            }
                        }
                    }
                    if (existingItem != null) break;
                }


                if (existingItem != null)
                {
                    // Update ALL cells of the EXISTING item
                    List<Vector2I> existingPositions = GetItemPositions(existingItem, pageItems);
                    int newCount = pageItems[existingPositions[0].X, existingPositions[0].Y].count + count;

                    foreach(var pos in existingPositions)
                    {
                        pageItems[pos.X, pos.Y] = (existingItem, newCount);
                    }
                    
                    if (item != existingItem)
                    {
                        item.QueueFree();
                    }
                }
                else
                {
                    // Normal Placement of NEW item
                    for (int relX = 0; relX < itemShape.SizeX; relX++)
                    {
                        for (int relY = 0; relY < itemShape.SizeZ; relY++)
                        {
                            if (!itemShape.IsOccupied(relX, 0, relY)) continue;

                            int gridX = x + relX;
                            int gridY = y + relY;

                            if (IsValidCell(gridX, gridY))
                            {
                                pageItems[gridX, gridY] = (item, count);
                            }
                        }
                    }
                    item.currentGrid = this;
                    EmitSignal(SignalName.ItemAdded, this, item);
                }
            }
        }
        else
        {
            if (IsValidCell(x, y))
            {
                if(pageItems[x, y].item != null && pageItems[x, y].item.ItemData.ItemID == item.ItemData.ItemID && _inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                {
                     pageItems[x, y].count += count;
                     if (item != pageItems[x,y].item) item.QueueFree();
                }
                else
                {
                     pageItems[x, y] = (item, count);
                     item.currentGrid = this;
                     EmitSignal(SignalName.ItemAdded,this, item);
                }
            }
        }

        EmitSignal(SignalName.InventoryChanged);
    }

    public bool TryAddItem(Item item, int count)
    {
        if (item is UnitBodyItem && item.currentGrid != null) return false;
        return AddItem(item, count);
    }

    public bool TryAddItemAt(Item item, Vector2I position, int count)
    {
        if (item is UnitBodyItem && item.currentGrid != null) return false;
        if (CanAddItemAt(position.X, position.Y, item, count, out string reason))
        {
            AddItemAt(position.X, position.Y, item, count, _currentPageIndex);
            return true;
        }

        GD.Print(reason);
        return false;
    }

    private void RemoveItem(Item item, int count)
    {
        if (item == null) return;

        if (!TryFindItemPage(item, out _, out var pageItems)) return;

        List<Vector2I> positions = GetItemPositions(item, pageItems);

        // Determine the current stack size from the first found position
        var firstPos = positions[0];
        int currentStackSize = pageItems[firstPos.X, firstPos.Y].count;
        
        int newStackSize = currentStackSize - count;
        
        if (newStackSize < 0) newStackSize = 0;

        bool itemRemovedCompletely = (newStackSize == 0);

        foreach (Vector2I pos in positions)
        {
             if (itemRemovedCompletely)
             {
                 pageItems[pos.X, pos.Y] = (null, 0);
             }
             else
             {
                 // Update the stack size for this cell to match the new total
                 pageItems[pos.X, pos.Y] = (item, newStackSize);
             }
        }

        if (itemRemovedCompletely)
        {
            if (item.currentGrid == this)
                item.currentGrid = null;
        }

        RemoveEmptyTrailingPages();
        _isCacheDirty = true;
        EmitSignal(SignalName.ItemRemoved,this, item);
        EmitSignal(SignalName.InventoryChanged);
    }

    public bool TryRemoveItem(Item item, int count)
    {
        if (item is UnitBodyItem && count != 1) return false;
        if (!HasItem(item)) return false;
        RemoveItem(item, count);
        return true;
    }

    /// <summary>
    /// Refreshes inventory observers after instance state changes without moving
    /// the item (for example, when a weapon fires or reloads).
    /// </summary>
    public void NotifyItemChanged()
    {
        EmitSignal(SignalName.InventoryChanged);
    }


    public void ClearInventory()
    {
        foreach (var itemInfo in UniqueItems)
        {
            if (itemInfo.item != null && itemInfo.item.currentGrid == this)
                itemInfo.item.currentGrid = null;
        }

        _pages.Clear();
        _pages.Add(CreateEmptyPage());
        _currentPageIndex = 0;
        _uniqueItemsCache = null;
        _isCacheDirty = true;
        EmitSignal(SignalName.InventoryChanged);
    }

    public Godot.Collections.Dictionary<string, Variant> SaveContents()
    {
        var items = new Godot.Collections.Array<
            Godot.Collections.Dictionary<string, Variant>>();
        var visited = new HashSet<Item>();

        for (int pageIndex = 0; pageIndex < _pages.Count; pageIndex++)
        {
            var page = _pages[pageIndex];
            for (int x = 0; x < page.GetLength(0); x++)
            {
                for (int y = 0; y < page.GetLength(1); y++)
                {
                    (Item item, int count) itemInfo = page[x, y];
                    if (itemInfo.item?.ItemData == null ||
                        itemInfo.count <= 0 ||
                        !visited.Add(itemInfo.item))
                        continue;

                    var positions = new Godot.Collections.Array<Vector2I>();
                    for (int itemX = 0; itemX < page.GetLength(0); itemX++)
                    {
                        for (int itemY = 0; itemY < page.GetLength(1); itemY++)
                        {
                            if (ReferenceEquals(
                                    page[itemX, itemY].item,
                                    itemInfo.item))
                                positions.Add(new Vector2I(itemX, itemY));
                        }
                    }

                    var entry = new Godot.Collections.Dictionary<string, Variant>
                    {
                        ["page"] = pageIndex,
                        ["count"] = itemInfo.count,
                        ["item_id"] = itemInfo.item.ItemData.ItemID,
                        ["positions"] = positions
                    };
                    if (itemInfo.item.IsRangedWeapon)
                        entry["loaded_ammo"] = itemInfo.item.CurrentAmmo;
                    if (itemInfo.item is UnitBodyItem body)
                    {
                        entry["body_unit_id"] = body.UnitId;
                        entry["body_unit_name"] = body.UnitName;
                        entry["body_dead"] = body.IsDead;
                    }
                    items.Add(entry);
                }
            }
        }

        return new Godot.Collections.Dictionary<string, Variant>
        {
            ["current_page"] = _currentPageIndex,
            ["items"] = items
        };
    }

    public void LoadContents(
        Godot.Collections.Dictionary<string, Variant> data)
    {
        Initialize();
        if (_pages.Count == 0 || data == null ||
            !data.TryGetValue("items", out Variant itemsValue) ||
            itemsValue.VariantType != Variant.Type.Array)
            return;

        var items = itemsValue.AsGodotArray<
            Godot.Collections.Dictionary<string, Variant>>();
        var consumedLegacyEntries = new HashSet<int>();
        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            if (consumedLegacyEntries.Contains(itemIndex)) continue;
            Godot.Collections.Dictionary<string, Variant> entry =
                items[itemIndex];
            if (!entry.TryGetValue("item_id", out Variant itemIdValue) ||
                !entry.TryGetValue("count", out Variant countValue))
                continue;

            int itemId = itemIdValue.AsInt32();
            int count = countValue.AsInt32();
            int pageIndex = entry.TryGetValue("page", out Variant pageValue)
                ? pageValue.AsInt32()
                : 0;
            if (count <= 0 || pageIndex < 0) continue;

            Item item;
            if (entry.TryGetValue("body_unit_id", out Variant bodyId))
            {
                item = UnitBodyItem.CreateSaved(bodyId.AsString(),
                    entry["body_unit_name"].AsString(), entry["body_dead"].AsBool());
                count = 1;
            }
            else
            {
                var definition = InventoryManager.Instance?.GetItemData(itemId);
                if (definition == null) continue;
                item = ItemData.CreateItem(definition);
            }
            ItemData itemData = item.ItemData;
            if (item == null) continue;
            if (entry.TryGetValue("loaded_ammo", out Variant ammoValue))
                item.RestoreAmmo(ammoValue.AsInt32());

            while (_pages.Count <= pageIndex)
                _pages.Add(CreateEmptyPage());

            var positions = new Godot.Collections.Array<Vector2I>();
            if (entry.TryGetValue("positions", out Variant positionsValue) &&
                positionsValue.VariantType == Variant.Type.Array)
            {
                positions = positionsValue.AsGodotArray<Vector2I>();
            }
            else if (entry.TryGetValue("x", out Variant xValue) &&
                     entry.TryGetValue("y", out Variant yValue))
            {
                var savedPosition = new Vector2I(
                    xValue.AsInt32(),
                    yValue.AsInt32());
                positions.Add(savedPosition);
                consumedLegacyEntries.Add(itemIndex);

                int expectedPositions = 1;
                GridShape itemShape = itemData.ItemShape;
                if (InventorySettings.HasFlag(
                        Enums.InventorySettings.UseItemSizes) &&
                    itemShape != null)
                {
                    expectedPositions = 0;
                    for (int shapeX = 0; shapeX < itemShape.SizeX; shapeX++)
                    {
                        for (int shapeY = 0; shapeY < itemShape.SizeZ; shapeY++)
                        {
                            if (itemShape.IsOccupied(shapeX, 0, shapeY))
                                expectedPositions++;
                        }
                    }
                    expectedPositions = Math.Max(1, expectedPositions);
                }

                while (positions.Count < expectedPositions)
                {
                    int connectedIndex = -1;
                    Vector2I connectedPosition = Vector2I.Zero;
                    for (int candidateIndex = itemIndex + 1;
                         candidateIndex < items.Count;
                         candidateIndex++)
                    {
                        if (consumedLegacyEntries.Contains(candidateIndex))
                            continue;
                        var candidate = items[candidateIndex];
                        if (!candidate.TryGetValue("item_id", out Variant candidateItemId) ||
                            candidateItemId.AsInt32() != itemId ||
                            !candidate.TryGetValue("count", out Variant candidateCount) ||
                            candidateCount.AsInt32() != count ||
                            candidate.ContainsKey("positions") ||
                            !candidate.TryGetValue("x", out Variant candidateX) ||
                            !candidate.TryGetValue("y", out Variant candidateY))
                            continue;

                        int candidatePage = candidate.TryGetValue(
                            "page",
                            out Variant candidatePageValue)
                            ? candidatePageValue.AsInt32()
                            : 0;
                        if (candidatePage != pageIndex) continue;

                        var candidatePosition = new Vector2I(
                            candidateX.AsInt32(),
                            candidateY.AsInt32());
                        bool connected = false;
                        foreach (Vector2I position in positions)
                        {
                            Vector2I offset = candidatePosition - position;
                            if (Math.Abs(offset.X) + Math.Abs(offset.Y) != 1)
                                continue;
                            connected = true;
                            break;
                        }
                        if (!connected) continue;

                        connectedIndex = candidateIndex;
                        connectedPosition = candidatePosition;
                        break;
                    }

                    if (connectedIndex < 0) break;
                    consumedLegacyEntries.Add(connectedIndex);
                    positions.Add(connectedPosition);
                }
            }

            var page = _pages[pageIndex];
            bool canPlace = positions.Count > 0;
            foreach (Vector2I position in positions)
            {
                if (!IsValidCell(position.X, position.Y) ||
                    page[position.X, position.Y].item != null)
                {
                    canPlace = false;
                    break;
                }
            }

            if (!canPlace)
            {
                item.QueueFree();
                continue;
            }

            foreach (Vector2I position in positions)
                page[position.X, position.Y] = (item, count);

            item.currentGrid = this;
            EmitSignal(SignalName.ItemAdded, this, item);
        }

        int currentPage = data.TryGetValue(
            "current_page",
            out Variant currentPageValue)
            ? currentPageValue.AsInt32()
            : 0;
        _currentPageIndex = Mathf.Clamp(currentPage, 0, _pages.Count - 1);
        _uniqueItemsCache = null;
        _isCacheDirty = true;
        EmitSignal(SignalName.InventoryChanged);
    }

    public bool HasItem(Item item)
    {
        return TryFindItemPage(item, out _, out _);
    }

    private bool TryFindItemPage(
        Item item,
        out int pageIndex,
        out (Item item, int count)[,] pageItems)
    {
        pageIndex = -1;
        pageItems = null;
        if (item == null) return false;

        for (int page = 0; page < _pages.Count; page++)
        {
            var candidatePage = _pages[page];
            for (int x = 0; x < GridShape.SizeX; x++)
            {
                for (int y = 0; y < GridShape.SizeZ; y++)
                {
                    if (candidatePage[x, y].item != item) continue;

                    pageIndex = page;
                    pageItems = candidatePage;
                    return true;
                }
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
        return GetItemPositions(item, Items);
    }

    private List<Vector2I> GetItemPositions(Item item, (Item item, int count)[,] pageItems)
    {
        List<Vector2I> result = new();

        if (item == null || pageItems == null) return result;

        for (int x = 0; x < GridShape.SizeX; x++)
        {
            for (int y = 0; y < GridShape.SizeZ; y++)
            {
                if (pageItems[x, y].item == item)
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

    private void RemoveEmptyTrailingPages()
    {
        while (_pages.Count > 1 && !PageHasItems(_pages[^1]))
            _pages.RemoveAt(_pages.Count - 1);

        if (_currentPageIndex >= _pages.Count)
            _currentPageIndex = _pages.Count - 1;
    }

    private static bool PageHasItems((Item item, int count)[,] pageItems)
    {
        foreach (var itemInfo in pageItems)
        {
            if (itemInfo.item != null) return true;
        }

        return false;
    }

    #endregion

    #region CanAdd Validation Logic

    public bool CanAddItem(Item item, out Vector2I position, int count, out string reason)
    {
        return TryFindItemPlacement(item, count, out _, out position, out reason);
    }

    private bool TryFindItemPlacement(
        Item item,
        int count,
        out int pageIndex,
        out Vector2I position,
        out string reason)
    {
        pageIndex = -1;
        position = new Vector2I(-1, -1);
        reason = "N/A";

        if (item?.ItemData == null)
        {
            reason = "Item or ItemData is null";
            return false;
        }

        if (_pages.Count == 0)
        {
            reason = "Inventory has not been initialized";
            return false;
        }

        // Prefer the visible page, then use any existing overflow page.
        if (TryFindPositionOnPage(_pages[_currentPageIndex], item, count, out position, out reason))
        {
            pageIndex = _currentPageIndex;
            return true;
        }

        if (AllowsMultiplePages)
        {
            for (int page = 0; page < _pages.Count; page++)
            {
                if (page == _currentPageIndex) continue;
                if (TryFindPositionOnPage(_pages[page], item, count, out position, out reason))
                {
                    pageIndex = page;
                    return true;
                }
            }

            var emptyPage = CreateEmptyPage();
            if (TryFindPositionOnPage(emptyPage, item, count, out position, out reason))
            {
                pageIndex = _pages.Count;
                return true;
            }
        }

        GD.Print($"CanAddItem: No valid position found. Last reason: {reason}");
        return false;
    }

    private bool TryFindPositionOnPage(
        (Item item, int count)[,] pageItems,
        Item item,
        int count,
        out Vector2I position,
        out string reason)
    {
        position = new Vector2I(-1, -1);
        reason = "No valid position found";

        GridShape itemShape = item.ItemData?.ItemShape;
        int width = InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null
            ? itemShape.SizeX
            : 1;
        int height = InventorySettings.HasFlag(Enums.InventorySettings.UseItemSizes) && itemShape != null
            ? itemShape.SizeZ
            : 1;

        for (int x = 0; x <= GridShape.SizeX - width; x++)
        {
            for (int y = 0; y <= GridShape.SizeZ - height; y++)
            {
                if (!CanAddItemAt(x, y, item, count, pageItems, out reason)) continue;

                position = new Vector2I(x, y);
                return true;
            }
        }

        return false;
    }

    public bool CanAddItemAt(int x, int y, Item item, int count, out string reason)
    {
        return CanAddItemAt(x, y, item, count, Items, out reason);
    }

    private bool CanAddItemAt(
        int x,
        int y,
        Item item,
        int count,
        (Item item, int count)[,] pageItems,
        out string reason)
    {
        reason = "";

        if (item?.ItemData == null || pageItems == null)
        {
            reason = "Item, ItemData, or inventory page is null.";
            return false;
        }

        if (item is UnitBodyItem bodyItem && (count != 1 || OwningUnit?.UnitId == bodyItem.UnitId || HasItem(item) ||
            UniqueItems.Exists(entry => entry.item is UnitBodyItem existing && existing.UnitId == bodyItem.UnitId)))
        {
            reason = "A body is a unique item and cannot be duplicated or stacked.";
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

            for (int relX = 0; relX < itemShape.SizeX; relX++)
            {
                for (int relY = 0; relY < itemShape.SizeZ; relY++)
                {
                    if (!itemShape.IsOccupied(relX, 0, relY)) continue;

                    int gridX = x + relX;
                    int gridY = y + relY;

                    if (!IsValidCell(gridX, gridY))
                    {
                        reason = $"Position ({gridX}, {gridY}) is invalid or outside bounds.";
                        return false;
                    }

                    if (pageItems[gridX, gridY].item != null)
                    {
                        if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                        {
                            if (pageItems[gridX, gridY].item.ItemData.ItemID == item.ItemData.ItemID &&
                                pageItems[gridX, gridY].count + count <= item.ItemData.MaxStackSize)
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
            if (!IsValidForPlacement(x, y))
            {
                reason = "Cell is outside boundaries or not part of inventory shape.";
                return false;
            }

            if (pageItems[x, y].item != null)
            {
                if (_inventorySettings.HasFlag(Enums.InventorySettings.AllowItemStacking))
                {
                    if (pageItems[x, y].item.ItemData.ItemID == item.ItemData.ItemID &&
                        pageItems[x, y].count + count <= item.ItemData.MaxStackSize)
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

        if (source.HasItem(item))
        {
            // The item still exists in source (partial removal/split).
            Item newItem = Managers.InventoryManager.Instance.InstantiateItem(item.ItemData);
            destination.AddItem(newItem, count); }
        else
        {
            // Item fully removed from source, safe to move the instance.
            destination.AddItem(item, count);
        }
        
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

        // We use the original item reference for validation and removal
        Item originalItem = data.item;

        if (!dest.CanAddItemAt(dstPos.X, dstPos.Y, originalItem, data.count, out var reason))
        {
            GD.Print($"Transfer blocked by destination: {reason}");
            return false;
        }

        source.RemoveItem(originalItem, data.count);

        if (source.HasItem(originalItem))
        {
            // Split occurred. Source kept original. Dest gets new.
            Item newItem = Managers.InventoryManager.Instance.InstantiateItem(originalItem.ItemData);
            dest.AddItemAt(dstPos, newItem, data.count);
            item = newItem;
        }
        else
        {
            // Moved.
            dest.AddItemAt(dstPos, originalItem, data.count);
            item = originalItem;
        }

        return true;
    }

    public static bool TryTransferItemAt(
        InventoryGrid source, Vector2I srcPos,
        InventoryGrid dest, Vector2I dstPos)
    {
        return TryTransferItemAt(source, srcPos, dest, dstPos, out _);
    }
    
    
    public Vector2I GetItemRootPos(Item item)
    {
	    if (item == null) return Vector2I.Zero;
	    
	    var positions = GetItemPositions(item);
	    return positions.Count > 0 ? positions[0] : Vector2I.Zero;
    }

    #endregion
}
