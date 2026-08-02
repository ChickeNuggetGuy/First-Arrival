using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// Runtime-built globe window for hiring scientists and assigning them to the
/// player team's research projects. Research stays globe-owned so scene
/// transitions never create a second copy of team progress.
/// </summary>
public partial class ResearchWindowUI : Control
{
	private GlobeTeamHolder teamHolder;
	private Label scientistSummaryLabel;
	private Label capacityHintLabel;
	private Button hireButton;
	private Button dismissButton;
	private VBoxContainer projectList;
	private readonly List<TeamBaseCellDefinition> subscribedBases = new();
	private bool pausedByWindow;
	private bool wasPausedBeforeWindow;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = 100;
		ProcessMode = ProcessModeEnum.Always;
		BuildWindow();
		Hide();
	}

	public override void _Input(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			CloseWindow();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _ExitTree()
	{
		RestorePauseState();
		DisconnectTeam();
		base._ExitTree();
	}

	public void ShowFor(GlobeTeamHolder holder)
	{
		if (holder == null) return;
		if (teamHolder != holder)
		{
			DisconnectTeam();
			teamHolder = holder;
			ConnectTeam();
		}

		PauseSimulation();
		Refresh();
		Show();
		hireButton?.GrabFocus();
	}

	private void BuildWindow()
	{
		var dimmer = new ColorRect
		{
			Color = new Color(0.015f, 0.025f, 0.045f, 0.86f),
			MouseFilter = MouseFilterEnum.Stop
		};
		dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dimmer);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(820, 620),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
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
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		var header = new HBoxContainer();
		var title = new Label
		{
			Text = "RESEARCH",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		header.AddChild(title);

		var closeButton = new Button
		{
			Text = "Close",
			TooltipText = "Close research (Esc)"
		};
		closeButton.Pressed += CloseWindow;
		header.AddChild(closeButton);
		content.AddChild(header);

		scientistSummaryLabel = new Label();
		scientistSummaryLabel.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(scientistSummaryLabel);

		var personnelControls = new HBoxContainer();
		hireButton = new Button { Text = "Hire 1 Scientist" };
		hireButton.Pressed += HireScientist;
		personnelControls.AddChild(hireButton);

		dismissButton = new Button { Text = "Dismiss 1 Idle Scientist" };
		dismissButton.Pressed += DismissScientist;
		personnelControls.AddChild(dismissButton);
		content.AddChild(personnelControls);

		capacityHintLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(capacityHintLabel);
		content.AddChild(new HSeparator());

		var sectionTitle = new Label { Text = "PROJECTS" };
		sectionTitle.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(sectionTitle);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		content.AddChild(scroll);

		projectList = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		projectList.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(projectList);

		var footer = new Label
		{
			Text = "Each assigned scientist removes 1 research point per day.",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		content.AddChild(footer);
	}

	private void ConnectTeam()
	{
		if (teamHolder == null) return;
		teamHolder.ScientistsChanged += TeamResearchChanged;
		teamHolder.ResearchProgressChanged += ProjectProgressChanged;
		teamHolder.ResearchProjectCompleted += ProjectCompleted;
		teamHolder.BaseAdded += TeamBasesChanged;
		teamHolder.BaseRemoved += TeamBasesChanged;
		RefreshBaseSubscriptions();
	}

	private void DisconnectTeam()
	{
		if (teamHolder != null)
		{
			teamHolder.ScientistsChanged -= TeamResearchChanged;
			teamHolder.ResearchProgressChanged -= ProjectProgressChanged;
			teamHolder.ResearchProjectCompleted -= ProjectCompleted;
			teamHolder.BaseAdded -= TeamBasesChanged;
			teamHolder.BaseRemoved -= TeamBasesChanged;
		}

		DisconnectBases();
		teamHolder = null;
	}

	private void RefreshBaseSubscriptions()
	{
		DisconnectBases();
		if (teamHolder?.Bases == null) return;
		foreach (TeamBaseCellDefinition baseDefinition in teamHolder.Bases)
		{
			if (baseDefinition == null) continue;
			baseDefinition.FacilityAdded += FacilityCapacityChanged;
			baseDefinition.FacilityCompleted += FacilityCapacityChanged;
			subscribedBases.Add(baseDefinition);
		}
	}

	private void DisconnectBases()
	{
		foreach (TeamBaseCellDefinition baseDefinition in subscribedBases)
		{
			baseDefinition.FacilityAdded -= FacilityCapacityChanged;
			baseDefinition.FacilityCompleted -= FacilityCapacityChanged;
		}
		subscribedBases.Clear();
	}

	private void Refresh()
	{
		if (teamHolder == null || projectList == null) return;

		int capacity = teamHolder.ScientistCapacity;
		int assigned = teamHolder.AssignedScientists;
		int idle = teamHolder.UnassignedScientists;
		int dismissible = Math.Max(0, teamHolder.HiredScientists - assigned);
		int inactive = Math.Max(0, teamHolder.HiredScientists - capacity);
		scientistSummaryLabel.Text =
			$"Scientists: {teamHolder.HiredScientists}/{capacity} hired  •  " +
			$"{assigned} assigned  •  {idle} idle" +
			(inactive > 0 ? $"  •  {inactive} inactive (over capacity)" : string.Empty);

		hireButton.Disabled = teamHolder.HiredScientists >= capacity;
		dismissButton.Disabled = dismissible <= 0;
		capacityHintLabel.Text = capacity <= 0
			? "Build and complete a Research Laboratory before hiring scientists."
			: "Research Laboratories provide scientist slots. Only idle scientists " +
			  "can be dismissed or assigned to a new project.";

		ClearProjectList();
		ResearchDatabase database = teamHolder.ResearchDatabase;
		if (database?.Projects == null || database.Projects.Count == 0)
		{
			projectList.AddChild(new Label
			{
				Text = "No research projects have been configured."
			});
			return;
		}

		foreach (ResearchProject project in database.Projects)
		{
			if (project != null) AddProjectCard(project);
		}
	}

	private void AddProjectCard(ResearchProject project)
	{
		string projectId = project.GetStableId();
		ResearchProjectStatus status = teamHolder.GetResearchProjectStatus(projectId);
		teamHolder.TryGetExistingResearchProgress(
			projectId,
			out ResearchProjectProgress progress);
		int initialPoints = progress?.InitialPoints ?? Math.Max(1, project.TotalResearchPoints);
		int remainingPoints = progress?.RemainingPoints ?? initialPoints;
		int assigned = progress?.AssignedScientists ?? 0;

		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		projectList.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		panel.AddChild(margin);

		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 5);
		margin.AddChild(card);

		var titleRow = new HBoxContainer();
		var title = new Label
		{
			Text = project.DisplayName,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		title.AddThemeFontSizeOverride("font_size", 18);
		titleRow.AddChild(title);
		titleRow.AddChild(new Label { Text = FormatStatus(status) });
		card.AddChild(titleRow);

		if (!string.IsNullOrWhiteSpace(project.Description))
		{
			card.AddChild(new Label
			{
				Text = project.Description,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			});
		}

		string prerequisiteText = GetPrerequisiteText(project);
		if (!string.IsNullOrEmpty(prerequisiteText))
		{
			card.AddChild(new Label
			{
				Text = prerequisiteText,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			});
		}

		string resultText = GetResultText(project);
		if (!string.IsNullOrEmpty(resultText))
		{
			card.AddChild(new Label
			{
				Text = resultText,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			});
		}

		var progressBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = initialPoints,
			Value = initialPoints - remainingPoints,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 18)
		};
		card.AddChild(progressBar);

		var assignmentRow = new HBoxContainer();
		string pointText = status == ResearchProjectStatus.Completed
			? "Complete"
			: $"{remainingPoints:N0}/{initialPoints:N0} points remaining";
		var progressLabel = new Label
		{
			Text = $"{assigned} assigned  •  {pointText}",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		assignmentRow.AddChild(progressLabel);

		var removeButton = new Button
		{
			Text = "−",
			TooltipText = "Unassign one scientist",
			Disabled = assigned <= 0
		};
		removeButton.Pressed += () => teamHolder?.TryUnassignScientists(projectId, 1);
		assignmentRow.AddChild(removeButton);

		int projectMaximum = project.MaxAssignedScientists <= 0
			? int.MaxValue
			: project.MaxAssignedScientists;
		var addButton = new Button
		{
			Text = "+",
			TooltipText = "Assign one idle scientist",
			Disabled = status is ResearchProjectStatus.Locked or
				ResearchProjectStatus.Completed or ResearchProjectStatus.Missing ||
				teamHolder.UnassignedScientists <= 0 ||
				assigned >= projectMaximum
		};
		addButton.Pressed += () => teamHolder?.TryAssignScientists(projectId, 1);
		assignmentRow.AddChild(addButton);
		card.AddChild(assignmentRow);
	}

	private static string GetPrerequisiteText(ResearchProject project)
	{
		if (project.Prerequisites == null || project.Prerequisites.Count == 0)
			return string.Empty;

		var names = new StringBuilder();
		foreach (ResearchProject prerequisite in project.Prerequisites)
		{
			if (prerequisite == null) continue;
			if (names.Length > 0) names.Append(", ");
			names.Append(prerequisite.DisplayName);
		}
		return names.Length == 0 ? string.Empty : $"Requires: {names}";
	}

	private static string GetResultText(ResearchProject project)
	{
		if (project.ResearchResults == null || project.ResearchResults.Count == 0)
			return string.Empty;

		var rewards = new List<string>();
		foreach (ResearchResult result in project.ResearchResults)
		{
			switch (result)
			{
				case UnlockItemsResult unlockResult:
					foreach (var item in unlockResult.UnlockedItems)
					{
						if (item != null && !string.IsNullOrWhiteSpace(item.ItemName))
							rewards.Add(item.ItemName);
					}
					break;
				case GrantFundsResult fundsResult when fundsResult.Amount > 0:
					rewards.Add($"${fundsResult.Amount:N0}");
					break;
				case TriggerResearchEventResult eventResult
					when !string.IsNullOrWhiteSpace(eventResult.EventId):
					rewards.Add($"Event: {eventResult.EventId}");
					break;
			}
		}
		return rewards.Count == 0
			? string.Empty
			: $"Results: {string.Join(", ", rewards)}";
	}

	private static string FormatStatus(ResearchProjectStatus status) => status switch
	{
		ResearchProjectStatus.Active => "IN PROGRESS",
		ResearchProjectStatus.Available => "AVAILABLE",
		ResearchProjectStatus.Completed => "COMPLETED",
		ResearchProjectStatus.Locked => "LOCKED",
		_ => "UNAVAILABLE"
	};

	private void ClearProjectList()
	{
		foreach (Node child in projectList.GetChildren())
		{
			projectList.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void HireScientist()
	{
		teamHolder?.TryHireScientists(1);
	}

	private void DismissScientist()
	{
		teamHolder?.TryDismissScientists(1);
	}

	private void CloseWindow()
	{
		Hide();
		RestorePauseState();
	}

	private void PauseSimulation()
	{
		SceneTree tree = GetTree();
		if (tree == null || pausedByWindow) return;
		wasPausedBeforeWindow = tree.Paused;
		if (!wasPausedBeforeWindow)
		{
			tree.Paused = true;
			pausedByWindow = true;
		}
	}

	private void RestorePauseState()
	{
		if (!pausedByWindow) return;
		SceneTree tree = GetTree();
		if (tree != null)
			tree.Paused = wasPausedBeforeWindow;
		pausedByWindow = false;
	}

	private void TeamResearchChanged(
		GlobeTeamHolder holder,
		int hired,
		int capacity,
		int assigned)
	{
		if (Visible) Refresh();
	}

	private void ProjectProgressChanged(
		GlobeTeamHolder holder,
		string projectId,
		int remainingPoints,
		int assigned)
	{
		if (Visible) Refresh();
	}

	private void ProjectCompleted(GlobeTeamHolder holder, string projectId)
	{
		if (Visible) Refresh();
	}

	private void TeamBasesChanged(int cellIndex, GlobeTeamHolder holder)
	{
		RefreshBaseSubscriptions();
		if (Visible) Refresh();
	}

	private void FacilityCapacityChanged(FacilityConstruction construction)
	{
		if (Visible) Refresh();
	}
}
