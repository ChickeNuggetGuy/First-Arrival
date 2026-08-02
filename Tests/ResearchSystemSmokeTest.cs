using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FirstArrival.Scripts.Inventory_System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

public partial class ResearchSystemSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			await RunTest();
			GD.Print("RESEARCH_SMOKE_TEST_PASSED");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"RESEARCH_SMOKE_TEST_FAILED: {exception}");
			GetTree().Quit(1);
		}
	}

	private async Task RunTest()
	{
		ValidateLiveCatalog();

		ResearchProject firstProject = CreateProject("standard_sidearms", 6);
		ResearchProject secondProject = CreateProject("second", 10);
		secondProject.Prerequisites.Add(firstProject);

		var fundsResult = new GrantFundsResult();
		SetProperty(fundsResult, nameof(GrantFundsResult.Amount), 1_000L);
		firstProject.ResearchResults.Add(fundsResult);

		ItemData lockedItem = ResourceLoader.Load<ItemData>(
			"res://Data/Items/M9_Pistol_Item.tres");
		Assert(lockedItem != null && !lockedItem.AvailableAtCampaignStart,
			"The locked item fixture did not load.");
		var unlockResult = new UnlockItemsResult();
		unlockResult.UnlockedItems.Add(lockedItem);
		firstProject.ResearchResults.Add(unlockResult);

		var eventResult = new TriggerResearchEventResult();
		SetProperty(eventResult, nameof(TriggerResearchEventResult.EventId), "first_complete");
		firstProject.ResearchResults.Add(eventResult);

		var database = new ResearchDatabase();
		database.Projects.Add(firstProject);
		database.Projects.Add(secondProject);
		Assert(database.GetValidationErrors().Count == 0,
			"The valid test database failed validation.");

		TeamBaseCellDefinition baseDefinition = CreateBaseWithLaboratory();
		var holder = new GlobeTeamHolder(
			Enums.UnitTeam.Player,
			new List<TeamBaseCellDefinition> { baseDefinition },
			100);
		AddChild(holder);
		holder.ConfigureResearchDatabase(database);

		Assert(holder.ScientistCapacity == 5, "Laboratory capacity was not counted.");
		Assert(holder.TryHireScientists(5), "Could not hire up to laboratory capacity.");
		Assert(!holder.TryHireScientists(1), "Hiring exceeded laboratory capacity.");
		Assert(holder.GetResearchProjectStatus("second") == ResearchProjectStatus.Locked,
			"A prerequisite-gated project started available.");
		Assert(!holder.TryAssignScientists("second", 1),
			"Scientists were assigned to a locked project.");
		Assert(holder.TryAssignScientists("standard_sidearms", 2),
			"Scientists could not be assigned to an available project.");

		holder.AdvanceResearch(2);
		Assert(holder.TryGetResearchProgress("standard_sidearms", out var progress) &&
			progress.RemainingPoints == 2,
			"Two scientists over two days did not produce four points.");

		Godot.Collections.Dictionary<string, Variant> legacySave = holder.Save();
		legacySave.Remove("research");
		legacySave.Remove("unlockedItemIds");
		var legacyHolder = new GlobeTeamHolder();
		AddChild(legacyHolder);
		legacyHolder.ConfigureResearchDatabase(database);
		await legacyHolder.LoadAsync(legacySave, this);
		Assert(legacyHolder.HiredScientists == 0 &&
			!legacyHolder.IsItemUnlocked(lockedItem),
			"A legacy save without research fields did not load safe defaults.");

		Godot.Collections.Dictionary<string, Variant> midwaySave = holder.Save();
		var loadedHolder = new GlobeTeamHolder();
		AddChild(loadedHolder);
		loadedHolder.ConfigureResearchDatabase(database);
		await loadedHolder.LoadAsync(midwaySave, this);
		Assert(loadedHolder.ScientistCapacity == 5 &&
			loadedHolder.HiredScientists == 5 &&
			loadedHolder.AssignedScientists == 2,
			"Scientist capacity, hires, or assignments did not survive save/load.");
		Assert(loadedHolder.TryGetResearchProgress("standard_sidearms", out progress) &&
			progress.RemainingPoints == 2,
			"Mid-project points did not survive save/load.");

		loadedHolder.AdvanceResearch(1);
		Assert(loadedHolder.IsResearchProjectCompleted("standard_sidearms"),
			"The project did not complete at exactly zero points.");
		Assert(loadedHolder.funds == 1_100,
			"The funds result was not applied exactly once.");
		Assert(loadedHolder.IsItemUnlocked(lockedItem),
			"The item result did not unlock its item.");
		GameManager.Instance.SetCurrentTeamResearchState(
			new[] { lockedItem.ItemID },
			Array.Empty<string>());
		Assert(!GameManager.Instance.IsItemUnlocked(lockedItem),
			"An item ID bypassed its required completed research project.");
		Assert(!BuySellUI.IsItemAvailableForPurchase(lockedItem),
			"The market exposed an item whose research was incomplete.");
		GameManager.Instance.SetCurrentTeamResearchState(
			loadedHolder.GetUnlockedItemIdsSnapshot(),
			loadedHolder.GetCompletedResearchProjectIdsSnapshot());
		Assert(GameManager.Instance.IsItemUnlocked(lockedItem),
			"A completed project's item was not purchasable at the base.");
		Assert(BuySellUI.IsItemAvailableForPurchase(lockedItem),
			"The market did not expose a legitimately unlocked item.");
		Assert(loadedHolder.TryConsumeResearchEvent(out string eventId) &&
			eventId == "first_complete",
			"The event result was not queued.");
		Assert(loadedHolder.GetResearchProjectStatus("second") ==
			ResearchProjectStatus.Available,
			"Completing a prerequisite did not unlock the next project.");

		loadedHolder.AdvanceResearch(50);
		Assert(loadedHolder.funds == 1_100,
			"Completion results were applied more than once.");
		Assert(loadedHolder.TryAssignScientists("second", 3),
			"Released scientists could not be reassigned.");
		Assert(!loadedHolder.TryDismissScientists(3),
			"Assigned scientists were dismissed.");
		Assert(loadedHolder.TryUnassignScientists("second", 1) &&
			loadedHolder.TryDismissScientists(3),
			"Idle scientists could not be dismissed.");

		Godot.Collections.Dictionary<string, Variant> completedSave = loadedHolder.Save();
		var completedLoad = new GlobeTeamHolder();
		AddChild(completedLoad);
		completedLoad.ConfigureResearchDatabase(database);
		await completedLoad.LoadAsync(completedSave, this);
		completedLoad.AdvanceResearch(10);
		Assert(completedLoad.funds == 1_100 && completedLoad.IsItemUnlocked(lockedItem),
			"A completed save repeated or lost its results.");

		var windowHolder = new GlobeTeamHolder();
		AddChild(windowHolder);
		windowHolder.ConfigureResearchDatabase(
			ResourceLoader.Load<ResearchDatabase>(
				"res://Data/Research/ResearchDatabase.tres"));
		int projectsBeforeOpening = GetSavedResearchProjectCount(windowHolder);

		var researchWindow = new ResearchWindowUI();
		AddChild(researchWindow);
		researchWindow.ShowFor(windowHolder);
		Assert(researchWindow.Visible,
			"The production research window could not display team research state.");
		Assert(GetTree().Paused,
			"The modal research window did not pause globe simulation.");
		Assert(GetSavedResearchProjectCount(windowHolder) == projectsBeforeOpening,
			"Opening research created runtime state for unstarted projects.");
		researchWindow.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Assert(!GetTree().Paused,
			"Closing the research window did not restore globe simulation.");
	}

	private static int GetSavedResearchProjectCount(GlobeTeamHolder holder)
	{
		Godot.Collections.Dictionary<string, Variant> saved = holder.Save();
		var research = saved["research"].AsGodotDictionary<string, Variant>();
		var projects = research["projects"].AsGodotDictionary<string, Variant>();
		return projects.Count;
	}

	private static void ValidateLiveCatalog()
	{
		ResearchDatabase liveDatabase = ResourceLoader.Load<ResearchDatabase>(
			"res://Data/Research/ResearchDatabase.tres");
		Assert(liveDatabase != null, "The live research database did not load.");
		Assert(liveDatabase.Projects.Count == 18,
			"The starter research catalog does not contain all 18 weapon projects.");
		Assert(liveDatabase.GetValidationErrors().Count == 0,
			"The live research database failed structural validation.");

		ItemDatabase itemDatabase = ResourceLoader.Load<ItemDatabase>(
			"res://Data/InventorySystem/ItemsDatabase.tres");
		Assert(itemDatabase != null, "The live item database did not load.");
		int lockedItemCount = 0;
		foreach (ItemData item in itemDatabase.GetAllItems())
		{
			if (item == null || item.AvailableAtCampaignStart) continue;
			lockedItemCount++;
			ResearchProject project = liveDatabase.GetProject(item.RequiredResearch);
			Assert(project != null,
				$"Locked item '{item.ItemName}' references missing research " +
				$"'{item.RequiredResearch}'.");

			bool projectUnlocksItem = false;
			foreach (ResearchResult result in project.ResearchResults)
			{
				if (result is not UnlockItemsResult unlockResult) continue;
				foreach (ItemData unlockedItem in unlockResult.UnlockedItems)
				{
					if (unlockedItem?.ItemID == item.ItemID)
					{
						projectUnlocksItem = true;
						break;
					}
				}
				if (projectUnlocksItem) break;
			}
			Assert(projectUnlocksItem,
				$"Research '{project.GetStableId()}' does not unlock " +
				$"'{item.ItemName}'.");
		}
		Assert(lockedItemCount == 18,
			"The locked starter weapon fixture count changed unexpectedly.");
	}

	private static ResearchProject CreateProject(string projectId, int points)
	{
		var project = new ResearchProject();
		SetProperty(project, nameof(ResearchProject.ProjectId), projectId);
		SetProperty(project, nameof(ResearchProject.DisplayName), projectId);
		SetProperty(project, nameof(ResearchProject.TotalResearchPoints), points);
		return project;
	}

	private static TeamBaseCellDefinition CreateBaseWithLaboratory()
	{
		var baseDefinition = new TeamBaseCellDefinition(
			1,
			"Test Base",
			Enums.UnitTeam.Player,
			null);
		FacilityDefinition laboratory = ResourceLoader.Load<FacilityDefinition>(
			"res://Data/Facilities/ResearchLaboratory.tres");
		Assert(laboratory != null, "The laboratory definition did not load.");
		FacilityConstruction construction = FacilityConstruction.Create(
			laboratory,
			Vector2I.Zero,
			"res://Scenes/BaseCells/ResearchLaboratoryCell.tscn",
			constructImmediately: true);
		Assert(baseDefinition.TryAddFacilityConstruction(construction),
			"The laboratory could not be added to the test base.");

		Godot.Collections.Dictionary<string, Variant> legacyFacility =
			construction.Save();
		legacyFacility.Remove("scientistCapacity");
		FacilityConstruction migratedFacility = FacilityConstruction.Load(
			legacyFacility);
		Assert(migratedFacility?.ScientistCapacity == 5,
			"A legacy laboratory save did not recover capacity from its definition.");

		legacyFacility["scientistCapacity"] = 0;
		FacilityConstruction explicitZeroFacility = FacilityConstruction.Load(
			legacyFacility);
		Assert(explicitZeroFacility?.ScientistCapacity == 0,
			"An explicit saved scientist capacity was not authoritative.");
		return baseDefinition;
	}

	private static void SetProperty(object target, string propertyName, object value)
	{
		PropertyInfo property = target.GetType().GetProperty(
			propertyName,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (property == null)
			throw new InvalidOperationException($"Property '{propertyName}' was not found.");
		property.SetValue(target, value);
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
