#if TOOLS
using Godot;
using System;

public partial class GridShapeInspectorPlugin : EditorInspectorPlugin
{
    private EditorUndoRedoManager _undoRedo;
    private int _currentLayerIndex = 0;

    public GridShapeInspectorPlugin() { }

    public GridShapeInspectorPlugin(EditorUndoRedoManager undoRedo)
    {
        _undoRedo = undoRedo;
    }

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is GridShape;
    }

    public override bool _ParseProperty(
        GodotObject @object,
        Variant.Type type,
        string name,
        PropertyHint hintType,
        string hintString,
        PropertyUsageFlags usage,
        bool wide
    )
    {
        // Changed: "ShapeGrid" -> "OccupiedCells"
        if (name.Equals("OccupiedCells", StringComparison.Ordinal) && @object is GridShape gridShape)
        {
            var container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 5);
            
            var layerTabs = new TabBar
            {
                TabCount = gridShape.SizeY,
                ScrollingEnabled = true,
                CustomMinimumSize = new Vector2(0, 32)
            };
            
            for (int i = 0; i < gridShape.SizeY; i++)
            {
                layerTabs.SetTabTitle(i, $"Layer {i}");
            }
            
            if (_currentLayerIndex >= gridShape.SizeY) _currentLayerIndex = 0;
            layerTabs.CurrentTab = _currentLayerIndex;
            container.AddChild(layerTabs);

            container.AddChild(new HSeparator());

            var infoLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            infoLabel.AddThemeColorOverride("font_color", Colors.LightGray);
            container.AddChild(infoLabel);

            // Pivot cell selector
            var pivotContainer = new HBoxContainer();
            pivotContainer.AddThemeConstantOverride("separation", 4);
            
            var pivotLabel = new Label { Text = "Pivot: " };
            pivotContainer.AddChild(pivotLabel);
            
            var pivotXSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = gridShape.SizeX - 1,
                Value = gridShape.PivotCell.X,
                TooltipText = "Pivot X",
                CustomMinimumSize = new Vector2(60, 0)
            };
            var pivotYSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = gridShape.SizeY - 1,
                Value = gridShape.PivotCell.Y,
                TooltipText = "Pivot Y (Layer)",
                CustomMinimumSize = new Vector2(60, 0)
            };
            var pivotZSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = gridShape.SizeZ - 1,
                Value = gridShape.PivotCell.Z,
                TooltipText = "Pivot Z",
                CustomMinimumSize = new Vector2(60, 0)
            };

            void UpdatePivot()
            {
                var newPivot = new Vector3I(
                    (int)pivotXSpin.Value,
                    (int)pivotYSpin.Value,
                    (int)pivotZSpin.Value
                );
                
                if (_undoRedo != null && gridShape.PivotCell != newPivot)
                {
                    var oldPivot = gridShape.PivotCell;
                    _undoRedo.CreateAction("Set Pivot Cell");
                    _undoRedo.AddDoProperty(gridShape, "PivotCell", newPivot);
                    _undoRedo.AddUndoProperty(gridShape, "PivotCell", oldPivot);
                    _undoRedo.CommitAction();
                }
            }

            pivotXSpin.ValueChanged += _ => UpdatePivot();
            pivotYSpin.ValueChanged += _ => UpdatePivot();
            pivotZSpin.ValueChanged += _ => UpdatePivot();

            pivotContainer.AddChild(pivotXSpin);
            pivotContainer.AddChild(pivotYSpin);
            pivotContainer.AddChild(pivotZSpin);
            container.AddChild(pivotContainer);

            container.AddChild(new HSeparator());

            var actionContainer = new HBoxContainer();
            actionContainer.AddThemeConstantOverride("separation", 4);
            
            var btnAll = new Button 
            { 
                Text = "All", 
                TooltipText = "Fill all cells in layer",
                CustomMinimumSize = new Vector2(60, 28)
            };
            var btnNone = new Button 
            { 
                Text = "None", 
                TooltipText = "Clear all cells in layer",
                CustomMinimumSize = new Vector2(60, 28)
            };
            var btnInvert = new Button 
            { 
                Text = "Invert", 
                TooltipText = "Invert all cells in layer",
                CustomMinimumSize = new Vector2(60, 28)
            };

            actionContainer.AddChild(btnAll);
            actionContainer.AddChild(btnNone);
            actionContainer.AddChild(btnInvert);
            container.AddChild(actionContainer);

            container.AddChild(new HSeparator());

            var gridContainer = new GridContainer
            {
                Columns = gridShape.SizeX
            };
            container.AddChild(gridContainer);

            void RefreshGridButtons()
            {
                foreach (Node child in gridContainer.GetChildren())
                    child.QueueFree();

                int currentY = _currentLayerIndex;
                infoLabel.Text = $"Layer {currentY} (X: {gridShape.SizeX}, Z: {gridShape.SizeZ})";

                // Update pivot spin box limits when grid resizes
                pivotXSpin.MaxValue = Math.Max(0, gridShape.SizeX - 1);
                pivotYSpin.MaxValue = Math.Max(0, gridShape.SizeY - 1);
                pivotZSpin.MaxValue = Math.Max(0, gridShape.SizeZ - 1);
                pivotXSpin.Value = gridShape.PivotCell.X;
                pivotYSpin.Value = gridShape.PivotCell.Y;
                pivotZSpin.Value = gridShape.PivotCell.Z;

                for (int z = 0; z < gridShape.SizeZ; z++)
                {
                    for (int x = 0; x < gridShape.SizeX; x++)
                    {
                        bool isPivot = (x == gridShape.PivotCell.X && 
                                       currentY == gridShape.PivotCell.Y && 
                                       z == gridShape.PivotCell.Z);

                        var cellCheck = new CheckBox
                        {
                            TooltipText = isPivot 
                                ? $"({x}, {currentY}, {z}) [PIVOT]" 
                                : $"({x}, {currentY}, {z})",
                            ButtonPressed = gridShape.IsOccupied(x, currentY, z)
                        };

                        // Highlight pivot cell
                        if (isPivot)
                        {
                            cellCheck.Modulate = new Color(0.5f, 1f, 0.5f);
                        }

                        int capX = x; 
                        int capZ = z;

                        cellCheck.Toggled += (bool toggled) =>
                        {
                            if (_undoRedo == null) return;
                            
                            // Changed: "SetGridShapeCell" -> "SetOccupied"
                            _undoRedo.CreateAction("Set Grid Cell");
                            _undoRedo.AddDoMethod(gridShape, "SetOccupied", capX, currentY, capZ, toggled);
                            _undoRedo.AddUndoMethod(gridShape, "SetOccupied", capX, currentY, capZ, !toggled);
                            _undoRedo.CommitAction();
                        };

                        gridContainer.AddChild(cellCheck);
                    }
                }
            }

            void ModifyCurrentLayer(int mode)
            {
                if (_undoRedo == null) return;

                string actionName = mode == 0 ? "Clear Layer" : (mode == 1 ? "Fill Layer" : "Invert Layer");
                _undoRedo.CreateAction(actionName);

                int currentY = _currentLayerIndex;
                bool changesMade = false;

                for (int z = 0; z < gridShape.SizeZ; z++)
                {
                    for (int x = 0; x < gridShape.SizeX; x++)
                    {
                        bool currentVal = gridShape.IsOccupied(x, currentY, z);
                        bool newVal = currentVal;

                        if (mode == 0) newVal = false;
                        else if (mode == 1) newVal = true;
                        else if (mode == 2) newVal = !currentVal;

                        if (currentVal != newVal)
                        {
                            // Changed: "SetGridShapeCell" -> "SetOccupied"
                            _undoRedo.AddDoMethod(gridShape, "SetOccupied", x, currentY, z, newVal);
                            _undoRedo.AddUndoMethod(gridShape, "SetOccupied", x, currentY, z, currentVal);
                            changesMade = true;
                        }
                    }
                }

                if (changesMade)
                {
                    _undoRedo.CommitAction();
                    RefreshGridButtons(); 
                }
            }

            btnNone.Pressed += () => ModifyCurrentLayer(0);
            btnAll.Pressed += () => ModifyCurrentLayer(1);
            btnInvert.Pressed += () => ModifyCurrentLayer(2);

            layerTabs.TabChanged += (long tab) =>
            {
                _currentLayerIndex = (int)tab;
                RefreshGridButtons();
            };

            RefreshGridButtons();
            AddCustomControl(container);
            return true;
        }

        return false;
    }
}
#endif