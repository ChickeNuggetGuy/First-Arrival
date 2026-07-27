using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;

public partial class CellDefinitionVisual : Node3D, IContextUser<CellDefinitionVisual>
{
	public int CellIndex;
	public HexCellDefinition parentCellDefinition;
	[Export] private CollisionObject3D collisionObject;
	[Export] private Label3D label;
	[Export(PropertyHint.Range, "0,20,1")] private int labelRevealRangeSteps = 2;

	private GlobeInputManager _inputManager;
	private bool _definitionVisible = true;
	public CellDefinitionVisual(HexCellDefinition parentCellDefinition, int cellIndex)
	{
		this.parentCellDefinition = parentCellDefinition;
		this.CellIndex = cellIndex;
	}
	
	public CellDefinitionVisual()
	{
		this.parentCellDefinition = null;
		this.CellIndex = -1;
	}

	public virtual Dictionary<string, Callable> GetContextActions()
	{
		Dictionary<string, Callable> contectActions = new Dictionary<string, Callable>();
		// contectActions.Add("Focus", Callable.From(GlobeC));
		return contectActions;
	}

	public override void _Ready()
	{
		ConnectInputManager();
		RefreshLabelVisibility();
	}

	public void BindDefinition(HexCellDefinition definition)
	{
		parentCellDefinition = definition;
		CellIndex = definition?.cellIndex ?? -1;
		if (label != null)
			label.Text = definition?.definitionName ?? string.Empty;
		RefreshLabelVisibility();
	}

	public void SetDefinitionVisible(bool visible)
	{
		_definitionVisible = visible;
		Visible = visible;
		ProcessMode = visible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
		if (collisionObject != null)
			collisionObject.InputRayPickable = visible;
		RefreshLabelVisibility();
	}

	private void ConnectInputManager()
	{
		if (_inputManager == GlobeInputManager.Instance) return;

		if (_inputManager != null)
			_inputManager.CurrentCellChanged -= OnCurrentCellChanged;

		_inputManager = GlobeInputManager.Instance;
		if (_inputManager != null)
			_inputManager.CurrentCellChanged += OnCurrentCellChanged;
	}

	private void OnCurrentCellChanged(HexCellData? _)
	{
		RefreshLabelVisibility();
	}

	private void RefreshLabelVisibility()
	{
		if (label == null) return;
		ConnectInputManager();
		label.Visible = _definitionVisible &&
			_inputManager != null &&
			_inputManager.IsCellNearCurrentCell(CellIndex, labelRevealRangeSteps);
	}

	public override void _ExitTree()
	{
		if (_inputManager != null)
			_inputManager.CurrentCellChanged -= OnCurrentCellChanged;
		_inputManager = null;
		parentCellDefinition?.ClearVisual(this);
		base._ExitTree();
	}

	public CellDefinitionVisual parent { get; set; }
}
