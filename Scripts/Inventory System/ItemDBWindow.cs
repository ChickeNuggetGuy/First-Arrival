#if TOOLS
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using FirstArrival.Scripts.Inventory_System;

[Tool]
public partial class ItemDBWindow : Control
{
    private const string PREF_DB_PATH = "firstarrival/item_db_last_path";

    // UI References
    private EditorFileDialog _fileDialog;
    private Label _pathLabel;
    private HSplitContainer _contentRoot; 
    private ItemList _itemList;
    private LineEdit _searchBar;
    private VBoxContainer _editorContainer;
    private Label _statusLabel;
    private MenuButton _addMenuBtn;

    // State
    private ItemDatabase _database;
    private ItemData _selectedItem;
    private string _currentDbPath;
    private bool _isCreatingNew = false;
    private List<Type> _itemTypes = new List<Type>();

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        LayoutUI();
        
        if (ProjectSettings.HasSetting(PREF_DB_PATH))
        {
            string lastPath = (string)ProjectSettings.GetSetting(PREF_DB_PATH);
            if (FileAccess.FileExists(lastPath)) OpenDatabase(lastPath);
        }
    }

    private void LayoutUI()
    {
        var mainLayout = new VBoxContainer();
        mainLayout.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(mainLayout);

        // --- TOOLBAR ---
        var toolbar = new HBoxContainer();
        mainLayout.AddChild(toolbar);
        
        var loadBtn = new Button { Text = "Load DB" };
        loadBtn.Pressed += OnLoadPressed;
        toolbar.AddChild(loadBtn);

        var newBtn = new Button { Text = "New DB" };
        newBtn.Pressed += OnNewPressed;
        toolbar.AddChild(newBtn);

        _pathLabel = new Label { Text = "No Database", Modulate = Colors.Gray };
        toolbar.AddChild(new Label { Text = " | " });
        toolbar.AddChild(_pathLabel);

        // --- DIALOG ---
        _fileDialog = new EditorFileDialog();
        _fileDialog.Access = EditorFileDialog.AccessEnum.Resources;
        _fileDialog.Filters = new string[] { "*.tres", "*.res" };
        _fileDialog.FileSelected += OnFileSelected;
        AddChild(_fileDialog);

        // --- SPLIT VIEW ---
        _contentRoot = new HSplitContainer();
        _contentRoot.SizeFlagsVertical = SizeFlags.ExpandFill;
        _contentRoot.Visible = false;
        _contentRoot.SplitOffset = 250;
        mainLayout.AddChild(_contentRoot);

        // --- LEFT (LIST) ---
        var leftPanel = new VBoxContainer();
        _contentRoot.AddChild(leftPanel);

        var actionRow = new HBoxContainer();
        _addMenuBtn = new MenuButton { Text = "Add New...", Flat = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _addMenuBtn.GetPopup().AboutToPopup += RefreshAddMenu;
        actionRow.AddChild(_addMenuBtn);
        
        var remBtn = new Button { Text = "Remove", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        remBtn.Pressed += OnRemoveItemPressed;
        actionRow.AddChild(remBtn);
        leftPanel.AddChild(actionRow);

        var rescanBtn = new Button { Text = "Force Rescan", Modulate = Colors.LightGreen };
        rescanBtn.Pressed += ForceRescan;
        leftPanel.AddChild(rescanBtn);

        _searchBar = new LineEdit { PlaceholderText = "Search..." };
        _searchBar.TextChanged += (t) => RefreshItemList(t);
        leftPanel.AddChild(_searchBar);

        _itemList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _itemList.ItemSelected += OnItemSelected;
        leftPanel.AddChild(_itemList);

        // --- RIGHT (DYNAMIC INSPECTOR) ---
        var rightPanel = new PanelContainer();
        _contentRoot.AddChild(rightPanel);
        
        var scroll = new ScrollContainer();
        rightPanel.AddChild(scroll);

        var rightContent = new VBoxContainer();
        rightContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(rightContent);

        // Container for Dynamic Fields
        _editorContainer = new VBoxContainer();
        _editorContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _editorContainer.CustomMinimumSize = new Vector2(300, 0);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddChild(_editorContainer);
        rightContent.AddChild(margin);

        var saveBtn = new Button { Text = "Save To Disk", CustomMinimumSize = new Vector2(0, 40) };
        saveBtn.Pressed += SaveSelectedToDisk;
        rightContent.AddChild(saveBtn);
        
        _statusLabel = new Label();
        rightContent.AddChild(_statusLabel);
    }

    // --- DYNAMIC INSPECTOR BUILDER ---
    private void OnItemSelected(long index)
    {
	    int id = (int)_itemList.GetItemMetadata((int)index);
	    _selectedItem = _database.GetItem(id);
	    
	    if (_selectedItem != null)
	    {
		    EditorInterface.Singleton.EditResource(_selectedItem);
	    }
    }

    private void RebuildInspector()
    {
        foreach (Node child in _editorContainer.GetChildren()) child.QueueFree();
        if (_selectedItem == null) return;

        // Header
        var header = new Label { 
            Text = $"{_selectedItem.GetType().Name} (ID: {_selectedItem.ItemID})", 
            ThemeTypeVariation = "HeaderLarge" 
        };
        _editorContainer.AddChild(header);
        _editorContainer.AddChild(new HSeparator());

        // Get Property List
        var properties = _selectedItem.GetPropertyList();

        foreach (var prop in properties)
        {
            string name = prop["name"].AsString();
            Variant.Type type = (Variant.Type)(int)prop["type"];
            PropertyUsageFlags usage = (PropertyUsageFlags)(int)prop["usage"];
            string hintString = prop["hint_string"].AsString();

            if ((usage & PropertyUsageFlags.Editor) == 0) continue;
            if (name == "script" || name == "resource_path" || name == "resource_name" || name == "ItemID") continue;

            Control control = null;
            bool useLabel = true;

            // SPECIAL: Array Handling
            if (type == Variant.Type.Array)
            {
                // We handle the layout internally for arrays, so don't use standard row layout
                BuildArrayEditor(name, hintString);
                continue; // Skip to next property
            }
            
            // Standard Property Handling
            if (name == "ItemName")
            {
                var le = new LineEdit();
                le.Text = _selectedItem.Get(name).AsString();
                le.TextChanged += (txt) => { _selectedItem.Set(name, txt); };
                control = le;
            }
            else
            {
                switch (type)
                {
                    case Variant.Type.Bool:
                        var cb = new CheckBox { Text = "Enabled" };
                        cb.ButtonPressed = _selectedItem.Get(name).AsBool();
                        cb.Toggled += (v) => _selectedItem.Set(name, v);
                        control = cb;
                        useLabel = false; // Checkbox has its own label
                        break;

                    case Variant.Type.Int:
                        if (!string.IsNullOrEmpty(hintString) && hintString.Contains(","))
                        {
                            var ob = new OptionButton();
                            string[] options = hintString.Split(",");
                            for(int i=0; i<options.Length; i++) ob.AddItem(options[i].Split(":")[0]); 
                            ob.Selected = _selectedItem.Get(name).AsInt32();
                            ob.ItemSelected += (idx) => _selectedItem.Set(name, (int)idx);
                            control = ob;
                        }
                        else
                        {
                            var sb = new SpinBox { Rounded = true, MaxValue = 99999, MinValue = -9999 };
                            sb.Value = _selectedItem.Get(name).AsInt32();
                            sb.ValueChanged += (v) => _selectedItem.Set(name, v);
                            control = sb;
                        }
                        break;

                    case Variant.Type.Float:
                        var fsb = new SpinBox { Step = 0.01f, MaxValue = 99999, MinValue = -9999 };
                        fsb.Value = _selectedItem.Get(name).AsSingle();
                        fsb.ValueChanged += (v) => _selectedItem.Set(name, v);
                        control = fsb;
                        break;

                    case Variant.Type.String:
                        if (hintString == "multiline") 
                        {
                            var te = new TextEdit { CustomMinimumSize = new Vector2(0, 80) };
                            te.Text = _selectedItem.Get(name).AsString();
                            te.TextChanged += () => _selectedItem.Set(name, te.Text);
                            control = te;
                        }
                        else
                        {
                            var le = new LineEdit();
                            le.Text = _selectedItem.Get(name).AsString();
                            le.TextChanged += (txt) => _selectedItem.Set(name, txt);
                            control = le;
                        }
                        break;
                    
                    case Variant.Type.Color:
                        var cp = new ColorPickerButton { CustomMinimumSize = new Vector2(0, 30) };
                        cp.Color = _selectedItem.Get(name).AsColor();
                        cp.ColorChanged += (c) => _selectedItem.Set(name, c);
                        control = cp;
                        break;

                    case Variant.Type.Object:
                        var picker = new EditorResourcePicker();
                        picker.BaseType = hintString; 
                        picker.EditedResource = _selectedItem.Get(name).As<Resource>();
                        picker.ResourceChanged += (res) => _selectedItem.Set(name, res);
                        control = picker;
                        break;

                    default:
                        var btn = new Button { Text = "Unrecognized Type: Edit in Inspector" };
                        btn.Pressed += () => EditorInterface.Singleton.EditResource(_selectedItem);
                        control = btn;
                        break;
                }
            }

            var row = new VBoxContainer();
            if (useLabel) row.AddChild(new Label { Text = PrettifyName(name), Modulate = Colors.LightGray });
            if (control != null) row.AddChild(control);
            _editorContainer.AddChild(row);
            _editorContainer.AddChild(new Control { CustomMinimumSize = new Vector2(0, 5) }); 
        }
    }
	
   private void BuildArrayEditor(string propertyName, string hintString)
    {
        // 1. Get the actual array
        var arrayVariant = _selectedItem.Get(propertyName);
        var array = arrayVariant.AsGodotArray(); 

        // 2. Container for the whole array UI
        var arrayBox = new VBoxContainer();
        _editorContainer.AddChild(arrayBox);

        // 3. Header
        var headerRow = new HBoxContainer();
        var label = new Label { Text = $"{PrettifyName(propertyName)} (Size: {array.Count})" };
        headerRow.AddChild(label);
        arrayBox.AddChild(headerRow);
        
        // 4. Determine Type contained in Array (from hint_string "24/17:ClassName")
        string containedType = "Resource"; 
        if (!string.IsNullOrEmpty(hintString) && hintString.Contains(":")) 
            containedType = hintString.Split(":")[1];

        // 5. Draw List Items
        var listContainer = new VBoxContainer();
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddChild(listContainer);
        arrayBox.AddChild(margin);

        for (int i = 0; i < array.Count; i++)
        {
            int index = i; // Capture for closure
            var row = new HBoxContainer();
            
            row.AddChild(new Label { Text = $"{i}:", CustomMinimumSize = new Vector2(20, 0) });

            var picker = new EditorResourcePicker();
            picker.BaseType = containedType;
            picker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            
            // Safe casting from Variant to Resource
            var itemVariant = array[i];
            picker.EditedResource = itemVariant.Obj as Resource;
            
            picker.ResourceChanged += (newRes) => {
                var currArray = _selectedItem.Get(propertyName).AsGodotArray();
                // FIX: Handle null vs object for Variant
                currArray[index] = newRes == null ? new Variant() : Variant.From(newRes);
                _selectedItem.Set(propertyName, currArray);
            };

            row.AddChild(picker);

            var delBtn = new Button { Text = "X", Modulate = Colors.Red };
            delBtn.Pressed += () => {
                var currArray = _selectedItem.Get(propertyName).AsGodotArray();
                currArray.RemoveAt(index);
                _selectedItem.Set(propertyName, currArray);
                RebuildInspector(); 
            };
            row.AddChild(delBtn);

            listContainer.AddChild(row);
        }

        // 6. Add Button
        var addBtn = new Button { Text = "+ Add Element" };
        addBtn.Pressed += () => {
             var currArray = _selectedItem.Get(propertyName).AsGodotArray();
             
             // FIX: Pass 'new Variant()' instead of 'null'
             currArray.Add(new Variant()); 
             
             _selectedItem.Set(propertyName, currArray);
             RebuildInspector();
        };
        arrayBox.AddChild(addBtn);

        _editorContainer.AddChild(new HSeparator());
    }
    private string PrettifyName(string camelCase)
    {
        return System.Text.RegularExpressions.Regex.Replace(camelCase, "([a-z])([A-Z])", "$1 $2");
    }

    // --- SAVING ---
    private void SaveSelectedToDisk()
    {
        if (_selectedItem == null) return;
        string currentName = _selectedItem.ItemName;
        string safeName = currentName.Trim().Replace(" ", "_");
        _selectedItem.ResourceName = $"{currentName}_Item";

        string currentPath = _selectedItem.ResourcePath;
        string dir = string.IsNullOrEmpty(currentPath) ? _database.DirectoryPath : currentPath.GetBaseDir();
        string newFileName = $"{safeName}_Item.tres";
        string newPath = $"{dir}/{newFileName}";

        if (currentPath != newPath)
        {
             if (FileAccess.FileExists(newPath)) { _statusLabel.Text = "Error: Name Collision!"; return; }
             ResourceSaver.Save(_selectedItem, newPath);
             _selectedItem.TakeOverPath(newPath);
             if (FileAccess.FileExists(currentPath)) DirAccess.RemoveAbsolute(currentPath);
        }
        else
        {
            ResourceSaver.Save(_selectedItem);
        }

        _statusLabel.Text = "Saved " + newFileName;
        RefreshItemList(_searchBar.Text);
    }

    // --- BOILERPLATE ---
    private void OnLoadPressed() { _isCreatingNew = false; _fileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile; _fileDialog.Title = "Open DB"; _fileDialog.PopupCenteredRatio(0.5f); }
    private void OnNewPressed() { _isCreatingNew = true; _fileDialog.FileMode = EditorFileDialog.FileModeEnum.SaveFile; _fileDialog.Title = "Create DB"; _fileDialog.CurrentFile = "ItemDatabase.tres"; _fileDialog.PopupCenteredRatio(0.5f); }
    private void OnFileSelected(string path) { if(_isCreatingNew) CreateDB(path); else OpenDatabase(path); }
    private void CreateDB(string path) { var db = new ItemDatabase(); db.DirectoryPath = path.GetBaseDir() + "/"; ResourceSaver.Save(db, path); OpenDatabase(path); }
    private void OpenDatabase(string path) {
        var res = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Replace);
        if (res is ItemDatabase db) {
            _database = db; _currentDbPath = path; _pathLabel.Text = path; _contentRoot.Visible = true;
            ProjectSettings.SetSetting(PREF_DB_PATH, path); ProjectSettings.Save();
            if (string.IsNullOrEmpty(_database.DirectoryPath)) { _database.DirectoryPath = path.GetBaseDir()+"/"; ResourceSaver.Save(_database); }
            if (_database.Items == null || _database.Items.Count == 0) ForceRescan();
            RefreshItemList();
        }
    }
    private void ForceRescan() { if (_database != null) { _database.Set("UpdateDatabase", true); ResourceSaver.Save(_database); RefreshItemList(); } }
    private void RefreshItemList(string filter = "") {
        if (_itemList == null || _database == null || _database.Items == null) return;
        _itemList.Clear();
        foreach (var kvp in _database.Items.OrderBy(k => k.Key)) {
            var item = kvp.Value; if (item == null) continue;
            string txt = $"[{item.ItemID}] {item.ItemName}";
            if (string.IsNullOrEmpty(filter) || txt.ToLower().Contains(filter.ToLower())) _itemList.SetItemMetadata(_itemList.AddItem(txt), item.ItemID);
        }
    }
    private void RefreshAddMenu() {
        var p = _addMenuBtn.GetPopup(); p.Clear(); _itemTypes.Clear();
        if (!p.IsConnected("id_pressed", new Callable(this, "OnAddMenuIDPressed"))) p.IdPressed += OnAddMenuIDPressed;
        var types = Assembly.GetAssembly(typeof(ItemData)).GetTypes().Where(t => (t == typeof(ItemData) || t.IsSubclassOf(typeof(ItemData))) && !t.IsAbstract).OrderBy(t=>t.Name);
        foreach(var t in types) { _itemTypes.Add(t); p.AddItem(t.Name); }
    }
    private void OnAddMenuIDPressed(long id) { CreateNewItem(_itemTypes[(int)id]); }
    private void CreateNewItem(Type type) {
        if (_database == null) return;
        if (_database.Items == null) _database.Items = new Godot.Collections.Dictionary<int, ItemData>();
        int id = _database.Items.Count > 0 ? _database.Items.Keys.Max() + 1 : 0;
        var item = (ItemData)Activator.CreateInstance(type);
        item.Set("ItemID", id); item.Set("ItemName", $"New {type.Name}");
        string dir = _database.DirectoryPath;
        if(string.IsNullOrEmpty(dir)) dir = _currentDbPath.GetBaseDir() + "/";
        string path = $"{dir}New_{type.Name}_{id}_Item.tres";
        ResourceSaver.Save(item, path);
        _database.Items.Add(id, item);
        ResourceSaver.Save(_database);
        RefreshItemList();
    }
    private void OnRemoveItemPressed() {
        if(_selectedItem == null) return;
        _database.Items.Remove(_selectedItem.ItemID);
        if(FileAccess.FileExists(_selectedItem.ResourcePath)) DirAccess.RemoveAbsolute(_selectedItem.ResourcePath);
        _selectedItem = null;
        foreach(Node n in _editorContainer.GetChildren()) n.QueueFree();
        ResourceSaver.Save(_database);
        RefreshItemList(_searchBar.Text);
    }
}
#endif