using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class BattleEndUI : UIWindow
{
	private MissionBattleResult _result;
	private Label _outcomeLabel;
	private Label _missionLabel;
	private Tree _summaryTree;
	private TabContainer _tabs;
	private VBoxContainer _recoveryTab;
	private ScrollContainer _recoveryScroll;
	private VBoxContainer _itemRows;
	private Label _cargoLabel;
	private Label _saleLabel;
	private Label _capacityWarning;
	private Button _continueButton;
	private bool _wasPaused;
	private bool _pausedByWindow;
	private bool _confirming;

	protected override Task _Setup()
	{
		ProcessMode = ProcessModeEnum.Always;
		BuildInterface();
		_continueButton.Pressed += ContinueButtonOnPressed;
		return Task.CompletedTask;
	}

	public async Task ShowResult(MissionBattleResult result)
	{
		if (result == null) throw new ArgumentNullException(nameof(result));
		_result = result;
		_confirming = false;
		PauseBattle();
		try
		{
			await ShowCall(false);
		}
		catch
		{
			RestorePauseState();
			throw;
		}
	}

	protected override Task DrawUI()
	{
		if (_result == null) return Task.CompletedTask;

		_outcomeLabel.Text = GetOutcomeText(_result.Outcome);
		_outcomeLabel.Modulate = _result.Outcome == Enums.MissionStatus.Successful
			? Colors.LightGreen
			: _result.Outcome == Enums.MissionStatus.Aborted
				? Colors.Gold
				: Colors.OrangeRed;
		_missionLabel.Text = _result.MissionName;
		DrawSummary();
		_tabs.SetTabHidden(1, _result.IsQuickBattle);
		_tabs.CurrentTab = !_result.IsQuickBattle && _result.IsOverCapacity
			? 1
			: 0;
		if (!_result.IsQuickBattle) RefreshRecoveryItems();
		_continueButton.Disabled = _result.IsOverCapacity;
		_continueButton.TooltipText = _result.IsOverCapacity
			? "Sell enough recovered cargo to get within capacity."
			: string.Empty;
		return Task.CompletedTask;
	}

	protected override void _Hide()
	{
		RestorePauseState();
	}

	public override void _ExitTree()
	{
		RestorePauseState();
		base._ExitTree();
	}

	private void BuildInterface()
	{
		MissionUITheme.Apply(this);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var background = new ColorRect
		{
			Color = MissionUITheme.BackdropColor,
			MouseFilter = MouseFilterEnum.Stop
		};
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(background);
		Visual = background;

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		background.AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(920, 570)
		};
		panel.AddThemeStyleboxOverride(
			"panel",
			MissionUITheme.CreatePanelStyle());
		center.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		panel.AddChild(margin);

		var content = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		_outcomeLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_outcomeLabel.AddThemeFontSizeOverride("font_size", 30);
		content.AddChild(_outcomeLabel);

		_missionLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_missionLabel.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(_missionLabel);
		content.AddChild(new HSeparator());

		_tabs = new TabContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		content.AddChild(_tabs);

		var summaryTab = new VBoxContainer { Name = "Summary" };
		_tabs.AddChild(summaryTab);
		_summaryTree = new Tree
		{
			Columns = 3,
			HideRoot = true,
			ColumnTitlesVisible = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_summaryTree.SetColumnTitle(0, "Category");
		_summaryTree.SetColumnTitle(1, "Count");
		_summaryTree.SetColumnTitle(2, "Points");
		_summaryTree.SetColumnExpand(0, true);
		_summaryTree.SetColumnExpand(1, false);
		_summaryTree.SetColumnExpand(2, false);
		_summaryTree.SetColumnCustomMinimumWidth(1, 100);
		_summaryTree.SetColumnCustomMinimumWidth(2, 120);
		summaryTab.AddChild(_summaryTree);

		_recoveryTab = new VBoxContainer { Name = "Recovered Items" };
		_recoveryTab.AddThemeConstantOverride("separation", 8);
		_tabs.AddChild(_recoveryTab);
		_recoveryScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_recoveryTab.AddChild(_recoveryScroll);
		_itemRows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_itemRows.AddThemeConstantOverride("separation", 6);
		_recoveryScroll.AddChild(_itemRows);

		var recoveryTotals = new HBoxContainer();
		recoveryTotals.AddThemeConstantOverride("separation", 20);
		_cargoLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_saleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
		recoveryTotals.AddChild(_cargoLabel);
		recoveryTotals.AddChild(_saleLabel);
		_recoveryTab.AddChild(recoveryTotals);
		_capacityWarning = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_recoveryTab.AddChild(_capacityWarning);

		content.AddChild(new HSeparator());
		var footer = new HBoxContainer();
		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_continueButton = new Button
		{
			Text = "Continue",
			CustomMinimumSize = new Vector2(160, 42)
		};
		footer.AddChild(spacer);
		footer.AddChild(_continueButton);
		content.AddChild(footer);
	}

	private void DrawSummary()
	{
		_summaryTree.Clear();
		TreeItem root = _summaryTree.CreateItem();
		AddSummaryRow(
			root,
			"Enemies killed",
			_result.Score.EnemiesKilled.ToString("N0"),
			_result.Score.EnemyKillPoints);
		AddSummaryRow(
			root,
			"Units lost",
			_result.Score.UnitsLost.ToString("N0"),
			_result.Score.UnitLossPoints);
		AddSummaryRow(
			root,
			GetOutcomeCategory(_result.Outcome),
			"—",
			_result.Score.OutcomePoints);
		TreeItem total = AddSummaryRow(
			root,
			"Total",
			string.Empty,
			_result.Score.TotalPoints);
		total.SetCustomColor(0, Colors.LightSkyBlue);
		total.SetCustomColor(2, Colors.LightSkyBlue);
	}

	private TreeItem AddSummaryRow(
		TreeItem root,
		string category,
		string count,
		int points)
	{
		TreeItem row = _summaryTree.CreateItem(root);
		row.SetText(0, category);
		row.SetText(1, count);
		row.SetText(2, FormatPoints(points));
		row.SetTextAlignment(1, HorizontalAlignment.Right);
		row.SetTextAlignment(2, HorizontalAlignment.Right);
		return row;
	}

	private void RefreshRecoveryItems()
	{
		int previousScroll = _recoveryScroll.ScrollVertical;
		ClearChildren(_itemRows);
		List<int> itemIds = GetRecoveredItemIds();
		if (itemIds.Count == 0)
		{
			_itemRows.AddChild(new Label
			{
				Text = "No items were recovered.",
				HorizontalAlignment = HorizontalAlignment.Center
			});
		}
		else
		{
			AddRecoveryHeader();
			foreach (int itemId in itemIds) AddRecoveryRow(itemId);
		}

		string capacity = _result.RecoveryWeightCapacity == long.MaxValue
			? "Unlimited"
			: _result.RecoveryWeightCapacity.ToString("N0");
		_cargoLabel.Text =
			$"Returning cargo: {_result.RecoveryWeight:N0} / {capacity} weight";
		_saleLabel.Text = $"Sale proceeds: ${_result.SaleProceeds:N0}";
		if (_result.IsOverCapacity)
		{
			long excess = _result.RecoveryWeight -
			              _result.RecoveryWeightCapacity;
			_capacityWarning.Text =
				$"Cargo is {excess:N0} weight over capacity. Sell recovered items to continue.";
			_capacityWarning.Modulate = Colors.OrangeRed;
		}
		else
		{
			_capacityWarning.Text = "Cargo is within capacity.";
			_capacityWarning.Modulate = Colors.LightGreen;
		}
		_continueButton.Disabled = _confirming || _result.IsOverCapacity;
		_continueButton.TooltipText = _result.IsOverCapacity
			? "Sell enough recovered cargo to get within capacity."
			: string.Empty;
		_recoveryScroll.SetDeferred("scroll_vertical", previousScroll);
	}

	private void AddRecoveryHeader()
	{
		var row = CreateRecoveryRowContainer();
		row.AddChild(new Control { CustomMinimumSize = new Vector2(30, 0) });
		row.AddChild(CreateColumnLabel("Item", 130, true));
		row.AddChild(CreateColumnLabel("Recovered", 70));
		row.AddChild(CreateColumnLabel("Returning", 70));
		row.AddChild(CreateColumnLabel("Sold", 55));
		row.AddChild(CreateColumnLabel("Weight", 60));
		row.AddChild(CreateColumnLabel("Sell value", 75));
		row.AddChild(CreateColumnLabel(string.Empty, 68));
		row.AddChild(CreateColumnLabel(string.Empty, 68));
		row.AddChild(CreateColumnLabel(string.Empty, 68));
		row.AddChild(CreateColumnLabel(string.Empty, 68));
		_itemRows.AddChild(row);
		_itemRows.AddChild(new HSeparator());
	}

	private void AddRecoveryRow(int itemId)
	{
		ItemData itemData = InventoryManager.Instance?.GetItemData(itemId);
		int returningCount = _result.Recovery.RecoveredItems.TryGetValue(
			itemId,
			out int recovered)
			? recovered
			: 0;
		int soldCount = _result.Recovery.SoldItems.TryGetValue(
			itemId,
			out int sold)
			? sold
			: 0;
		int originalCount = _result.Recovery.GetOriginalItemCount(itemId);
		long unitWeight = Math.Max(0, itemData?.weight ?? 0);
		long saleValue = Math.Max(0, itemData?.sellPrice ?? 0);

		var row = CreateRecoveryRowContainer();
		row.AddChild(new TextureRect
		{
			Texture = itemData?.ItemIcon,
			CustomMinimumSize = new Vector2(30, 30),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		});
		row.AddChild(CreateColumnLabel(
			itemData?.ItemName ?? $"Item {itemId}",
			130,
			true));
		row.AddChild(CreateColumnLabel(originalCount.ToString("N0"), 70));
		row.AddChild(CreateColumnLabel(returningCount.ToString("N0"), 70));
		row.AddChild(CreateColumnLabel(soldCount.ToString("N0"), 55));
		row.AddChild(CreateColumnLabel(unitWeight.ToString("N0"), 60));
		row.AddChild(CreateColumnLabel($"${saleValue:N0}", 75));

		var undoButton = CreateItemButton("Undo 1");
		undoButton.Disabled = soldCount == 0;
		undoButton.Pressed += () => ChangeSale(itemId, false, 1);
		row.AddChild(undoButton);

		var undoAllButton = CreateItemButton("Undo all");
		undoAllButton.Disabled = soldCount == 0;
		undoAllButton.Pressed += () =>
			ChangeSale(itemId, false, soldCount);
		row.AddChild(undoAllButton);

		var sellButton = CreateItemButton("Sell 1");
		sellButton.Disabled = returningCount == 0;
		sellButton.Pressed += () => ChangeSale(itemId, true, 1);
		row.AddChild(sellButton);

		var sellAllButton = CreateItemButton("Sell all");
		sellAllButton.Disabled = returningCount == 0;
		sellAllButton.Pressed += () =>
			ChangeSale(itemId, true, returningCount);
		row.AddChild(sellAllButton);
		_itemRows.AddChild(row);
	}

	private void ChangeSale(int itemId, bool sell, int count)
	{
		if (_confirming || _result?.Recovery == null) return;
		bool changed = sell
			? _result.Recovery.TrySellItem(itemId, count)
			: _result.Recovery.TryRestoreSoldItem(itemId, count);
		if (changed) RefreshRecoveryItems();
	}

	private List<int> GetRecoveredItemIds()
	{
		var uniqueIds = new HashSet<int>();
		foreach (int itemId in _result.Recovery.RecoveredItems.Keys)
			uniqueIds.Add(itemId);
		foreach (int itemId in _result.Recovery.SoldItems.Keys)
			uniqueIds.Add(itemId);

		var itemIds = new List<int>(uniqueIds);
		itemIds.Sort((left, right) => string.Compare(
			InventoryManager.Instance?.GetItemData(left)?.ItemName ?? left.ToString(),
			InventoryManager.Instance?.GetItemData(right)?.ItemName ?? right.ToString(),
			StringComparison.OrdinalIgnoreCase));
		return itemIds;
	}

	private async void ContinueButtonOnPressed()
	{
		if (_confirming || _result == null || _result.IsOverCapacity) return;
		_confirming = true;
		_continueButton.Disabled = true;
		bool completed = await GameManager.Instance.ConfirmBattleEnd(_result);
		if (completed || !GodotObject.IsInstanceValid(this)) return;
		_confirming = false;
		if (_result.IsQuickBattle)
			_continueButton.Disabled = false;
		else
			RefreshRecoveryItems();
	}

	private void PauseBattle()
	{
		if (_pausedByWindow) return;
		SceneTree tree = GetTree();
		_wasPaused = tree.Paused;
		tree.Paused = true;
		_pausedByWindow = true;
	}

	private void RestorePauseState()
	{
		if (!_pausedByWindow) return;
		SceneTree tree = GetTree();
		if (tree != null) tree.Paused = _wasPaused;
		_pausedByWindow = false;
	}

	private static HBoxContainer CreateRecoveryRowContainer()
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 6);
		return row;
	}

	private static Label CreateColumnLabel(
		string text,
		float minimumWidth,
		bool expand = false)
	{
		return new Label
		{
			Text = text,
			CustomMinimumSize = new Vector2(minimumWidth, 0),
			SizeFlagsHorizontal = expand
				? SizeFlags.ExpandFill
				: SizeFlags.ShrinkEnd,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = expand
				? HorizontalAlignment.Left
				: HorizontalAlignment.Right,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
		};
	}

	private static Button CreateItemButton(string text) => new()
	{
		Text = text,
		CustomMinimumSize = new Vector2(68, 30)
	};

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static string GetOutcomeText(Enums.MissionStatus outcome) =>
		outcome switch
		{
			Enums.MissionStatus.Successful => "MISSION ACCOMPLISHED",
			Enums.MissionStatus.Aborted => "MISSION ABANDONED",
			_ => "MISSION FAILED"
		};

	private static string GetOutcomeCategory(Enums.MissionStatus outcome) =>
		outcome switch
		{
			Enums.MissionStatus.Successful => "Mission success",
			Enums.MissionStatus.Aborted => "Mission abandoned",
			_ => "Mission failure"
		};

	private static string FormatPoints(int points) => points > 0
		? $"+{points:N0}"
		: points.ToString("N0");
}
