using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Globe.Countries;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

public sealed class CountryFundingReportEntry
{
	public uint CountryKey { get; }
	public string CountryName { get; }
	public float PlayerOpinion { get; }
	public long MonthlySupport { get; }
	public long SupportChange { get; }

	public CountryFundingReportEntry(
		uint countryKey,
		string countryName,
		float playerOpinion,
		long monthlySupport,
		long supportChange)
	{
		CountryKey = countryKey;
		CountryName = countryName;
		PlayerOpinion = playerOpinion;
		MonthlySupport = monthlySupport;
		SupportChange = supportChange;
	}
}

[GlobalClass]
public partial class GlobeTeamManager : Manager<GlobeTeamManager>
{
	private const string DefaultResearchDatabasePath =
		"res://Data/Research/ResearchDatabase.tres";
	private PackedScene baseScene;
	private Node baseContainer;
	[Export] private ResearchDatabase researchDatabase;
	
	public PackedScene shipScene;
	public Node shipContainer;
	
	public bool buildBaseMode { get; private set; }= false;
	public bool SendCraftMode { get; private set; }= false;
	private readonly HashSet<HexCellDefinition> _registeredDefinitions = new();
	private readonly System.Collections.Generic.Dictionary<TeamBaseCellDefinition, TeambasedVisual> _baseVisuals = new();

	[Export] public Enums.UnitTeam ViewingTeam { get; set; } = Enums.UnitTeam.Player;
	[Export] private bool scanForDefinitionsDaily = true;

	[ExportGroup("Country Funding")]
	[Export(PropertyHint.Range, "0,1000000000,10000,or_greater")]
	private long globalMonthlyFundingPool = 750_000;
	[Export(PropertyHint.Range, "0.1,1,0.05")]
	private double gdpFundingExponent = 0.75;
	[Export]
	private bool applyCountryOpinionToFunding = true;
	[Export(PropertyHint.Range, "0,1,0.05")]
	private double countryOpinionFundingEffect = 0.5;
	private readonly System.Collections.Generic.Dictionary<uint, long>
		_countryFundingBaseline = new();
	private List<CountryFundingReportEntry> _latestCompletedCountryFundingReport = new();

	private bool _timeSignalsConnected;

	private sealed class CountryFundingAllocation
	{
		public CountryRuntimeState Country { get; }
		public long BaseContribution { get; set; }
		public double FractionalRemainder { get; }

		public CountryFundingAllocation(
			CountryRuntimeState country,
			long baseContribution,
			double fractionalRemainder)
		{
			Country = country;
			BaseContribution = baseContribution;
			FractionalRemainder = fractionalRemainder;
		}
	}

	[Signal]
	public delegate void CraftDetectedEventHandler(
		Craft craft,
		int detectingTeam,
		int cellIndex);
	

	[Export] public bool overridePreviousInstance = false; 
	[Export] private Enums.UnitTeam teamsConfig = Enums.UnitTeam.None;
	
	[Export(PropertyHint.ResourceType,"Craft")] private Craft testCraft;
	

	[Export]private Godot.Collections.Dictionary<Enums.UnitTeam, GlobeTeamHolder> teamData = 
		new Godot.Collections.Dictionary<Enums.UnitTeam, GlobeTeamHolder>();
	
	public override string GetManagerName() => "GlobeTeamManager";


	public override void _Ready()
	{
		baseScene = ResourceLoader.Load<PackedScene>("res://Scenes/base.tscn");
		shipScene =  ResourceLoader.Load<PackedScene>("res://Scenes/ship.tscn");
		testCraft = ResourceLoader.Load<Craft>("res://Data/Items/Troop_Transport_Item.tres");
		researchDatabase ??=
			ResourceLoader.Load<ResearchDatabase>(DefaultResearchDatabasePath);
		researchDatabase?.ValidateAndReport();
		teamsConfig = Enums.UnitTeam.Player | Enums.UnitTeam.Enemy;
		ShouldExecuteOnlyOnce = true;
		base._Ready();
	}

	protected override async Task _Setup(bool loadingData)
	{
		teamData ??= new Godot.Collections.Dictionary<Enums.UnitTeam, GlobeTeamHolder>();
		foreach (GlobeTeamHolder existingHolder in teamData.Values)
			existingHolder?.ConfigureResearchDatabase(researchDatabase);
		
		// Only run default setup if we aren't loading existing data.
		if (loadingData && teamData.Count != 0) return;

		// Initialize default teams defined 
		foreach (var team in Enum.GetValues(typeof(Enums.UnitTeam)))
		{
			if (teamsConfig.HasFlag((Enums.UnitTeam)team))
			{
				if((Enums.UnitTeam)team == Enums.UnitTeam.None) continue;
				
				// Avoid duplicates if Setup runs multiple times
				if(teamData.ContainsKey((Enums.UnitTeam)team)) continue;
				
				var holder = new GlobeTeamHolder((Enums.UnitTeam)team, new List<TeamBaseCellDefinition>());
				holder.ConfigureResearchDatabase(researchDatabase);
				teamData[(Enums.UnitTeam)team] = holder;
				AddChild(holder);
			}
		}
	}

	protected override async Task _Execute(bool loadingData)
	{
		if (loadingData)
		{
			foreach (var teamHolder in GetAllTeamData().Values)
			{
				foreach (var baseDef in teamHolder.Bases)
					RegisterCellDefinition(baseDef);

				RestoreCraftVisuals(teamHolder);
				
				foreach (var baseDef in teamHolder.Bases)
				{
					if (!baseDef.GetAllCraftData().TryGetValue(
						    Enums.CraftStatus.Idle, out var idleCrafts))
						continue;

					// Iterate backwards since SendCraft mutates the list
					for (int i = idleCrafts.Count - 1; i >= 0; i--)
					{
						var craft = idleCrafts[i];
						if (craft.CurrentCellIndex != craft.HomeBaseIndex
						    && craft.CurrentCellIndex != -1)
						{
							if (teamHolder.Team == Enums.UnitTeam.Enemy &&
							    GlobeAIManager.Instance?.IsCraftAssigned(craft) == true)
								continue;

							// Fire-and-forget: craft will tween its way home
							_ = baseDef.SendCraft(
								craft.CurrentCellIndex,
								craft.HomeBaseIndex,
								craft,
								this,
								interactWithMission: teamHolder.Team == Enums.UnitTeam.Player,
								onArrived: teamHolder.Team == Enums.UnitTeam.Enemy
									? craft => GlobeAIManager.Instance?.OnAlienCraftArrived(craft)
									: null
							);
						}
					}
				}

				// Spawn bases as normal
				foreach (var baseDef in teamHolder.Bases)
				{
					var cell = GlobeHexGridManager.Instance
						.GetCellFromIndex(baseDef.cellIndex);
					if (cell.HasValue)
						SpawnBase(baseDef);
				}
			}
		}
		else
		{
			GlobeTeamHolder teamHolder = teamData[Enums.UnitTeam.Enemy];

			if (teamHolder != null)
			{
				HexCellData? randomCell = GlobeHexGridManager.Instance.GetRandomCell(true);

				if (randomCell.HasValue)
					TryBuildBase(
						Enums.UnitTeam.Enemy,
						randomCell.Value,
						teamHolder.Bases.Count + 1,
						0);
			}
		}

		TransferPendingBaseExpenditureToLedgers();
		EnsureCountryFundingBaseline();

		if (GlobeTimeManager.Instance != null && !_timeSignalsConnected)
		{
			GlobeTimeManager.Instance.DayChanged += OnDayChanged;
			GlobeTimeManager.Instance.MonthChanged += OnMonthChanged;
			_timeSignalsConnected = true;
		}
		await Task.CompletedTask;
	}

	public void SetBuildBaseMode(bool buildBaseMode)
	{
		this.buildBaseMode = buildBaseMode;
		if (buildBaseMode)
		{
			GlobeTimeManager.Instance.SetTimeSpeed(0);
		}
		else
		{
			GlobeTimeManager.Instance.SetTimeSpeed(1);
		}
	}
	
	#region Visual Restoration

	private void RestoreCraftVisuals(GlobeTeamHolder teamHolder)
	{
		if (teamHolder == null || teamHolder.Bases == null) return;

		foreach (TeamBaseCellDefinition baseDef in teamHolder.Bases)
		{
			var allCraft = baseDef.GetAllCraftData();

			foreach (var kvp in allCraft)
			{
				Enums.CraftStatus status = kvp.Key;
				Array<Craft> craftList = kvp.Value;

				foreach (Craft craft in craftList)
				{
					bool isEnRoute = status == Enums.CraftStatus.EnRoute;
					bool isAwayFromHome = craft.CurrentCellIndex != craft.HomeBaseIndex 
					                      && craft.CurrentCellIndex != -1;

					if (isEnRoute || isAwayFromHome)
					{
						SpawnCraftVisual(craft, teamHolder.Team);
					}
				}
			}
		}
	}
	
	private void SpawnCraftVisual(Craft craft, Enums.UnitTeam craftTeam)
	{
		if (craft == null) return;
    
		// FIX: Ensure the hex grid is ready
		if (GlobeHexGridManager.Instance == null)
		{
			GD.PrintErr("SpawnCraftVisual: GlobeHexGridManager not ready!");
			return;
		}

		MeshInstance3D shipNode = craft.visual;
		if (shipNode == null || !GodotObject.IsInstanceValid(shipNode))
		{
			shipNode = shipScene.Instantiate<MeshInstance3D>();
			craft.SetVisual(shipNode);
		}

		shipNode.Visible = craftTeam == ViewingTeam || craft.IsVisibleTo(ViewingTeam);

		if (shipContainer != null && shipNode.GetParent() != shipContainer)
			shipContainer.AddChild(shipNode);
		else if (shipNode.GetParent() == null)
			AddChild(shipNode);

		// Position at CurrentCellIndex (where it was when saved)
		var cellData = GlobeHexGridManager.Instance.GetCellFromIndex(craft.CurrentCellIndex);
		if (cellData.HasValue)
		{
			shipNode.GlobalPosition = cellData.Value.Center;
			DetectionRadiusVisualizer.AttachOrUpdate(
				shipNode,
				craft.CurrentCellIndex,
				craft.DetectionRadius,
				new Color(0.2f, 0.75f, 1.0f, 0.22f),
				craft.ShowDetectionRadius
			);

			// Orient towards target if one exists
			if (craft.TargetCellIndex != -1 && craft.TargetCellIndex != craft.CurrentCellIndex)
			{
				var targetCell = GlobeHexGridManager.Instance.GetCellFromIndex(craft.TargetCellIndex);
				if (targetCell.HasValue)
				{
					Vector3 upDir = shipNode.GlobalPosition.Normalized();
					shipNode.LookAt(targetCell.Value.Center, upDir);
				}
			}
		}
		else
		{
			GD.PrintErr($"SpawnCraftVisual: Could not find cell at index {craft.CurrentCellIndex}");
		}

		// Resume movement if still en route
		if (craft.Status == Enums.CraftStatus.EnRoute &&
		    craft.TargetCellIndex != -1 &&
		    craft.CurrentCellIndex != craft.TargetCellIndex)
		{
			_ = craft.GetBaseCellDefinition()?.SendCraft(
				craft.CurrentCellIndex, 
				craft.TargetCellIndex, 
				craft, 
				this,
				interactWithMission: craftTeam == Enums.UnitTeam.Player,
				onArrived: craftTeam == Enums.UnitTeam.Enemy
					? arrivedCraft => GlobeAIManager.Instance?.OnAlienCraftArrived(arrivedCraft)
					: null
			);
		}
	}

	#endregion

	#region Save / Load System

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var teamHolderData = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var kvp in teamData)
		{
			// FIX: Save the enum as its integer value, not its name
			teamHolderData[((int)kvp.Key).ToString()] = kvp.Value.Save();
		}

		var countryFundingBaseline =
			new Godot.Collections.Dictionary<string, Variant>();
		foreach (var contribution in _countryFundingBaseline)
			countryFundingBaseline[contribution.Key.ToString()] = contribution.Value;

		return new Godot.Collections.Dictionary<string, Variant>
		{
			["teamData"] = teamHolderData,
			["countryFundingBaseline"] = countryFundingBaseline
		};
	}

	public override async Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!HasLoadedData) return;

		_countryFundingBaseline.Clear();
		_latestCompletedCountryFundingReport.Clear();
		if (data.TryGetValue("countryFundingBaseline", out Variant baselineVariant) &&
		    baselineVariant.VariantType == Variant.Type.Dictionary)
		{
			var savedBaseline =
				baselineVariant.AsGodotDictionary<string, Variant>();
			foreach (var contribution in savedBaseline)
			{
				if (uint.TryParse(contribution.Key, out uint countryKey))
					_countryFundingBaseline[countryKey] =
						Math.Max(0, contribution.Value.AsInt64());
			}
		}

		Godot.Collections.Dictionary<string, Variant> teamsDict = null;

		if (data.ContainsKey("teamData"))
			teamsDict = data["teamData"].AsGodotDictionary<string, Variant>();
		else if (data.ContainsKey("teams"))
			teamsDict = data["teams"].AsGodotDictionary<string, Variant>();

		if (teamsDict != null)
		{
			foreach (var kvp in teamsDict)
			{
				if (!int.TryParse(kvp.Key, out int teamInt))
				{
					GD.PrintErr($"Failed to parse team key: {kvp.Key}");
					continue;
				}

				Enums.UnitTeam teamType = (Enums.UnitTeam)teamInt;
				var specificTeamSaveData = kvp.Value.AsGodotDictionary<string, Variant>();

				if (!teamData.ContainsKey(teamType))
				{
					GlobeTeamHolder newTeam = new GlobeTeamHolder();
					newTeam.Team = teamType;
					newTeam.ConfigureResearchDatabase(researchDatabase);
					AddChild(newTeam);
					teamData.Add(teamType, newTeam);
				}

				teamData[teamType].ConfigureResearchDatabase(researchDatabase);
				await teamData[teamType].LoadAsync(specificTeamSaveData, shipContainer ?? this);
			}
		}
		else
		{
			GD.PrintErr("GlobeTeamManager: No 'teamData' or 'teams' key found in save file.");
		}
	}
	#endregion

	#region Gameplay Input & Logic

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if(buildBaseMode)
		{
			if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed &&
			    mouseButton.ButtonIndex == MouseButton.Left)
			{
				HexCellData? cell = GlobeInputManager.Instance.CurrentCell;
				
				int baseIndex = GetTeamData(Enums.UnitTeam.Player).Bases.Count + 1;

				if (cell == null) return;

				if (TryBuildBase(Enums.UnitTeam.Player, cell.Value, baseIndex, 400000))
				{
					GD.Print("Building base");
				}
				else
				{
					GD.Print("Building failed");
				}
			}
		}

		if (SendCraftMode)
		{
			if (@event is InputEventMouseButton mouseButton
			    && mouseButton.Pressed
			    && mouseButton.ButtonIndex == MouseButton.Left
			)
			{
				HexCellData? cell = GlobeInputManager.Instance.CurrentCell;
				if (cell == null) return;
				if (cell.Value.cellType == Enums.HexGridType.Water)
				{
					GD.Print("Craft destinations must be on land.");
					return;
				}
				
				GlobeTeamHolder playerTeamHolder = GetTeamData(Enums.UnitTeam.Player);
				
				if (playerTeamHolder.SelectedCraft != null)
				{
					var selectedCraft = playerTeamHolder.SelectedCraft;
					TeamBaseCellDefinition baseDef = selectedCraft.GetBaseCellDefinition();
					
					if (baseDef == null)
					{
						foreach(var b in playerTeamHolder.Bases)
						{
							if (b.TryGetCraftFromIndex(selectedCraft.Index, out _))
							{
								baseDef = b;
								selectedCraft.SetBaseCellDefinition(b);
								break;
							}
						}
					}

					if (baseDef != null)
					{
						GD.Print("Send Craft Command Issued");
						_ = baseDef.SendCraft(selectedCraft.CurrentCellIndex, cell.Value.Index, selectedCraft, this);
						SetSendCraftMode(false, GetTeamData(Enums.UnitTeam.Player), null);
					}
					else
					{
						GD.PrintErr("Could not find Base Definition for selected craft.");
					}
				}
			}
		}
	}
	
	public bool TryBuildBase(Enums.UnitTeam team, HexCellData cell, int baseIndex , int cost)
	{
		if (team ==  Enums.UnitTeam.None) return false;
		if (cell.cellType == Enums.HexGridType.Water) return false;

		GlobeCityManager cityManager = GlobeCityManager.Instance;
		if (cityManager.TryGetCityDefinition(cell.Index, out var city)) return false;

		foreach (GlobeTeamHolder holder in teamData.Values)
		{
			if (holder?.Bases == null) continue;
			foreach (TeamBaseCellDefinition existingBase in holder.Bases)
			{
				if (existingBase.cellIndex == cell.Index)
					return false;
			}
		}

		if (!teamData.ContainsKey(team))
		{
			var holder = new GlobeTeamHolder(
				team,
				new List<TeamBaseCellDefinition>());
			holder.ConfigureResearchDatabase(researchDatabase);
			AddChild(holder);
			teamData.Add(team, holder);
		}
		
		if(!teamData[team].CanAffordCost(cost)) return false;

		if (teamData[team].TryBuildBase(cell, cost))
		{
			if (!teamData[team].TryGetBaseAtIndex(cell.Index, out var definition))
				return false;

			RegisterCellDefinition(definition);
			SpawnBase(definition);
			ScanForDefinitions(
				team,
				cell.Index,
				definition.DetectionRadius,
				definition.DetectionChance
			);
			buildBaseMode = false;
			return true;
		}
		return false;
	}
	
	private void SpawnBase(TeamBaseCellDefinition definition)
	{
		if (baseScene == null || definition == null) return;
		if (!definition.IsVisibleTo(ViewingTeam)) return;
		if (_baseVisuals.TryGetValue(definition, out var existing)
		    && GodotObject.IsInstanceValid(existing)) return;

		HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromIndex(definition.cellIndex);
		if (!cell.HasValue) return;

		var instance = baseScene.Instantiate<TeambasedVisual>();
		if (baseContainer != null) baseContainer.AddChild(instance);
		else AddChild(instance);

		instance.GlobalPosition = cell.Value.Center;
		Vector3 normal = cell.Value.Center.Normalized();
		Vector3 up = Mathf.Abs(normal.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
		instance.LookAt(cell.Value.Center + normal, up);
		instance.Name = definition.definitionName;
		definition.BindVisual(instance, ViewingTeam);
		_baseVisuals[definition] = instance;
		
		var label = instance.GetNodeOrNull<Label3D>("Label3D");
		if(label != null) label.Text = definition.definitionName;

		DetectionRadiusVisualizer.AttachOrUpdate(
			instance,
			definition.cellIndex,
			definition.DetectionRadius,
			GetTeamDetectionColor(definition.teamAffiliation),
			definition.ShowDetectionRadius
		);
	}

	public void RegisterCellDefinition(HexCellDefinition definition)
	{
		if (definition == null || !_registeredDefinitions.Add(definition)) return;
		definition.VisibilityChanged -= OnDefinitionVisibilityChanged;
		definition.VisibilityChanged += OnDefinitionVisibilityChanged;
		if (definition is TeamBaseCellDefinition teamBase)
		{
			teamBase.FacilityEffectsChanged -= OnBaseFacilityEffectsChanged;
			teamBase.FacilityEffectsChanged += OnBaseFacilityEffectsChanged;
		}
	}

	public void UnregisterCellDefinition(HexCellDefinition definition)
	{
		if (definition == null || !_registeredDefinitions.Remove(definition)) return;
		definition.VisibilityChanged -= OnDefinitionVisibilityChanged;
		if (definition is TeamBaseCellDefinition teamBase)
			teamBase.FacilityEffectsChanged -= OnBaseFacilityEffectsChanged;
	}

	/// <summary>
	/// Rolls once for each hidden hostile definition in the detector's hex-step range.
	/// Returns the definitions revealed by this scan.
	/// </summary>
	public List<HexCellDefinition> ScanForDefinitions(
		Enums.UnitTeam detectingTeam,
		int originCellIndex,
		int detectionRadius,
		float detectionChance)
	{
		var revealed = new List<HexCellDefinition>();
		if (detectingTeam == Enums.UnitTeam.None || detectionRadius < 0) return revealed;

		HexCellData? origin = GlobeHexGridManager.Instance?.GetCellFromIndex(originCellIndex);
		if (!origin.HasValue) return revealed;

		var cellsInRange = GlobeHexGridManager.Instance.GetCellsInStepRange(
			origin.Value,
			detectionRadius
		);
		var indicesInRange = new HashSet<int>();
		foreach (HexCellData cell in cellsInRange) indicesInRange.Add(cell.Index);

		float chance = Mathf.Clamp(detectionChance, 0.0f, 1.0f);
		foreach (HexCellDefinition definition in _registeredDefinitions)
		{
			if (definition == null || definition.IsVisibleTo(detectingTeam)) continue;
			if (!indicesInRange.Contains(definition.cellIndex)) continue;
			if (definition is TeamBaseCellDefinition teamBase
			    && teamBase.teamAffiliation == detectingTeam) continue;
			if (GD.Randf() > chance) continue;

			if (definition.RevealForTeam(detectingTeam))
				revealed.Add(definition);
		}

		return revealed;
	}

	public List<HexCellDefinition> ScanAllDetectors(Enums.UnitTeam detectingTeam)
	{
		var revealed = new List<HexCellDefinition>();
		GlobeTeamHolder holder = GetTeamData(detectingTeam);
		if (holder?.Bases == null) return revealed;

		foreach (TeamBaseCellDefinition baseDefinition in holder.Bases)
		{
			revealed.AddRange(ScanForDefinitions(
				detectingTeam,
				baseDefinition.cellIndex,
				baseDefinition.DetectionRadius,
				baseDefinition.DetectionChance
			));

			foreach (Craft craft in baseDefinition.CraftList)
			{
				if (craft == null || craft.Status == Enums.CraftStatus.Home) continue;
				revealed.AddRange(ScanForDefinitions(
					detectingTeam,
					craft.CurrentCellIndex,
					craft.DetectionRadius,
					craft.DetectionChance
				));
			}
		}

		return revealed;
	}

	/// <summary>
	/// Gives the viewing team a detection roll whenever a hostile craft enters a
	/// new cell. Detection persists on the craft and therefore survives saves.
	/// </summary>
	public bool TryDetectHostileCraft(
		Craft craft,
		Enums.UnitTeam craftTeam,
		int craftCellIndex)
	{
		if (craft == null || craftTeam == ViewingTeam ||
		    craft.IsVisibleTo(ViewingTeam))
			return false;

		GlobeTeamHolder detectingTeam = GetTeamData(ViewingTeam);
		if (detectingTeam?.Bases == null) return false;

		foreach (TeamBaseCellDefinition baseDefinition in detectingTeam.Bases)
		{
			if (RollCraftDetection(
				baseDefinition.cellIndex,
				baseDefinition.DetectionRadius,
				baseDefinition.DetectionChance,
				craftCellIndex))
				return RevealCraft(craft, craftCellIndex);

			foreach (Craft detectorCraft in baseDefinition.CraftList)
			{
				if (detectorCraft == null ||
				    detectorCraft.Status == Enums.CraftStatus.Home)
					continue;

				if (RollCraftDetection(
					detectorCraft.CurrentCellIndex,
					detectorCraft.DetectionRadius,
					detectorCraft.DetectionChance,
					craftCellIndex))
					return RevealCraft(craft, craftCellIndex);
			}
		}

		return false;
	}

	private bool RollCraftDetection(
		int detectorCellIndex,
		int detectionRadius,
		float detectionChance,
		int craftCellIndex)
	{
		if (detectionRadius < 0 || GD.Randf() > Mathf.Clamp(detectionChance, 0f, 1f))
			return false;

		HexCellData? detectorCell = GlobeHexGridManager.Instance?.GetCellFromIndex(
			detectorCellIndex);
		if (!detectorCell.HasValue) return false;

		foreach (HexCellData cell in GlobeHexGridManager.Instance.GetCellsInStepRange(
			detectorCell.Value,
			detectionRadius))
		{
			if (cell.Index == craftCellIndex) return true;
		}

		return false;
	}

	private bool RevealCraft(Craft craft, int cellIndex)
	{
		if (!craft.RevealForTeam(ViewingTeam)) return false;
		EmitSignal(SignalName.CraftDetected, craft, (int)ViewingTeam, cellIndex);
		if (DebugMode)
			GD.Print($"[Detection] {ViewingTeam} detected {craft.ItemName} at cell {cellIndex}.");
		return true;
	}

	private void OnDayChanged(
		int dayOfYear,
		int dayOfMonth,
		Enums.Day day,
		int daysAdvanced)
	{
		foreach (GlobeTeamHolder holder in teamData.Values)
		{
			if (holder == null) continue;
			holder.AdvanceResearch(daysAdvanced);
			if (holder.Bases == null) continue;
			foreach (TeamBaseCellDefinition baseDefinition in holder.Bases)
				baseDefinition?.AdvanceFacilityConstruction(daysAdvanced);
		}

		if (!scanForDefinitionsDaily) return;

		foreach (Enums.UnitTeam team in teamData.Keys)
			ScanAllDetectors(team);

		// Re-roll detection for hostile craft that are lingering at a scan point
		// or active mission rather than only checking them while they move.
		foreach (var teamEntry in teamData)
		{
			if (teamEntry.Key == ViewingTeam || teamEntry.Value?.Bases == null) continue;
			foreach (TeamBaseCellDefinition baseDefinition in teamEntry.Value.Bases)
			{
				foreach (Craft craft in baseDefinition.CraftList)
				{
					if (craft == null || craft.Status == Enums.CraftStatus.Home ||
					    craft.CurrentCellIndex < 0)
						continue;
					TryDetectHostileCraft(craft, teamEntry.Key, craft.CurrentCellIndex);
				}
			}
		}
	}

	private void OnMonthChanged(Enums.Month month)
	{
		GlobeTeamHolder playerTeam = GetTeamData(Enums.UnitTeam.Player);
		if (playerTeam != null && GlobeHexGridManager.Instance != null)
			ApplyMonthlyCountryFunding(playerTeam);

		foreach (GlobeTeamHolder holder in teamData.Values)
		{
			if (holder?.Bases == null) continue;

			long upkeep = 0;
			foreach (TeamBaseCellDefinition baseDefinition in holder.Bases)
				upkeep += baseDefinition?.MonthlyFacilityCost ?? 0;

			if (upkeep <= 0) continue;
			holder.ChangeFunds(-upkeep, "Facility upkeep");
		}
	}

	private void ApplyMonthlyCountryFunding(GlobeTeamHolder playerTeam)
	{
		System.Collections.Generic.Dictionary<uint, long> contributions =
			CalculateCountryFundingContributions();

		_latestCompletedCountryFundingReport = BuildCountryFundingReport(
			contributions,
			_countryFundingBaseline);
		ReplaceCountryFundingBaseline(contributions);

		foreach (CountryFundingReportEntry entry in _latestCompletedCountryFundingReport)
		{
			if (entry.MonthlySupport <= 0) continue;
			playerTeam.ChangeFunds(
				entry.MonthlySupport,
				$"{entry.CountryName} contribution");
		}
	}

	public List<CountryFundingReportEntry> GetCurrentCountryFundingReport()
	{
		EnsureCountryFundingBaseline();
		return BuildCountryFundingReport(
			CalculateCountryFundingContributions(),
			_countryFundingBaseline);
	}

	public List<CountryFundingReportEntry> GetLatestCompletedCountryFundingReport()
	{
		return _latestCompletedCountryFundingReport.Count > 0
			? new List<CountryFundingReportEntry>(_latestCompletedCountryFundingReport)
			: GetCurrentCountryFundingReport();
	}

	private void EnsureCountryFundingBaseline()
	{
		if (_countryFundingBaseline.Count > 0 ||
		    GlobeHexGridManager.Instance == null)
			return;

		ReplaceCountryFundingBaseline(CalculateCountryFundingContributions());
	}

	private void ReplaceCountryFundingBaseline(
		System.Collections.Generic.Dictionary<uint, long> contributions)
	{
		_countryFundingBaseline.Clear();
		foreach (var contribution in contributions)
			_countryFundingBaseline[contribution.Key] = contribution.Value;
	}

	private System.Collections.Generic.Dictionary<uint, long>
		CalculateCountryFundingContributions()
	{
		var contributions =
			new System.Collections.Generic.Dictionary<uint, long>();
		List<CountryRuntimeState> countries = GlobeHexGridManager.Instance?
			.GetCountryStatesSnapshot() ?? new List<CountryRuntimeState>();
		foreach (CountryRuntimeState country in countries)
			contributions[country.CountryKey] = 0;

		long fundingPool = Math.Max(0, globalMonthlyFundingPool);
		if (fundingPool == 0) return contributions;

		double exponent = Math.Clamp(gdpFundingExponent, 0.1, 1.0);
		double totalWeight = 0.0;
		var weightedCountries = new List<(CountryRuntimeState Country, double Weight)>();

		foreach (CountryRuntimeState country in countries)
		{
			double weight = country.GetFundingWeight(exponent);
			if (weight <= 0.0 || double.IsNaN(weight) || double.IsInfinity(weight))
				continue;

			weightedCountries.Add((country, weight));
			totalWeight += weight;
		}

		if (weightedCountries.Count == 0 ||
		    totalWeight <= 0.0 ||
		    double.IsNaN(totalWeight) ||
		    double.IsInfinity(totalWeight))
			return contributions;

		var allocations = new List<CountryFundingAllocation>(weightedCountries.Count);
		long distributed = 0;
		foreach (var weightedCountry in weightedCountries)
		{
			double exactContribution =
				fundingPool * (weightedCountry.Weight / totalWeight);
			long wholeContribution = (long)Math.Floor(exactContribution);
			allocations.Add(new CountryFundingAllocation(
				weightedCountry.Country,
				wholeContribution,
				exactContribution - wholeContribution));
			distributed += wholeContribution;
		}

		// Give any rounding remainder to the countries with the largest fractions.
		// With neutral opinion, this guarantees the full funding pool is distributed.
		long remaining = fundingPool - distributed;
		allocations.Sort((left, right) =>
		{
			int fractionComparison = right.FractionalRemainder.CompareTo(
				left.FractionalRemainder);
			return fractionComparison != 0
				? fractionComparison
				: string.Compare(
					left.Country.CountryName,
					right.Country.CountryName,
					StringComparison.OrdinalIgnoreCase);
		});
		for (int i = 0; i < remaining && i < allocations.Count; i++)
			allocations[i].BaseContribution++;

		allocations.Sort((left, right) => string.Compare(
			left.Country.CountryName,
			right.Country.CountryName,
			StringComparison.OrdinalIgnoreCase));

		double opinionEffect = Math.Clamp(countryOpinionFundingEffect, 0.0, 1.0);
		foreach (CountryFundingAllocation allocation in allocations)
		{
			double opinionMultiplier = 1.0;
			if (applyCountryOpinionToFunding)
			{
				double normalizedOpinion = Math.Clamp(
					allocation.Country.PlayerOpinion / 100.0,
					-1.0,
					1.0);
				opinionMultiplier += normalizedOpinion * opinionEffect;
			}

			double adjustedContribution =
				allocation.BaseContribution * opinionMultiplier;
			long contribution = adjustedContribution >= long.MaxValue
				? long.MaxValue
				: Math.Max(0, (long)Math.Round(
					adjustedContribution,
					MidpointRounding.AwayFromZero));
			contributions[allocation.Country.CountryKey] = contribution;
		}

		return contributions;
	}

	private static List<CountryFundingReportEntry> BuildCountryFundingReport(
		System.Collections.Generic.Dictionary<uint, long> contributions,
		System.Collections.Generic.Dictionary<uint, long> baseline)
	{
		var report = new List<CountryFundingReportEntry>();
		List<CountryRuntimeState> countries = GlobeHexGridManager.Instance?
			.GetCountryStatesSnapshot() ?? new List<CountryRuntimeState>();

		foreach (CountryRuntimeState country in countries)
		{
			long support = contributions.GetValueOrDefault(country.CountryKey);
			long change = baseline.TryGetValue(
				country.CountryKey,
				out long previousSupport)
				? ClampToLong((decimal)support - previousSupport)
				: 0;
			report.Add(new CountryFundingReportEntry(
				country.CountryKey,
				country.CountryName,
				country.PlayerOpinion,
				support,
				change));
		}

		return report;
	}

	private static long ClampToLong(decimal value) =>
		(long)Math.Clamp(value, long.MinValue, long.MaxValue);

	private void TransferPendingBaseExpenditureToLedgers()
	{
		foreach (GlobeTeamHolder holder in teamData.Values)
		{
			if (holder?.Bases == null) continue;
			foreach (TeamBaseCellDefinition baseDefinition in holder.Bases)
			{
				if (baseDefinition == null) continue;
				long expenditure =
					baseDefinition.ConsumeFacilityConstructionExpenditure();
				if (expenditure > 0)
				{
					holder.RecordMonthlyExpenditure(
						expenditure,
						$"Facility construction ({baseDefinition.definitionName})");
				}

				foreach (var income in baseDefinition.ConsumeBaseIncome())
					holder.RecordMonthlyIncome(income.Value, income.Key);
				foreach (var expense in baseDefinition.ConsumeBaseExpenditure())
					holder.RecordMonthlyExpenditure(expense.Value, expense.Key);
			}
		}
	}

	private void OnBaseFacilityEffectsChanged(
		TeamBaseCellDefinition definition)
	{
		if (definition == null ||
			!_baseVisuals.TryGetValue(definition, out TeambasedVisual visual) ||
			!GodotObject.IsInstanceValid(visual))
			return;

		DetectionRadiusVisualizer.AttachOrUpdate(
			visual,
			definition.cellIndex,
			definition.DetectionRadius,
			GetTeamDetectionColor(definition.teamAffiliation),
			definition.ShowDetectionRadius);
	}

	private void OnDefinitionVisibilityChanged(HexCellDefinition definition)
	{
		if (definition is not TeamBaseCellDefinition baseDefinition) return;

		if (definition.IsVisibleTo(ViewingTeam))
		{
			SpawnBase(baseDefinition);
			return;
		}

		if (_baseVisuals.Remove(baseDefinition, out var visual)
		    && GodotObject.IsInstanceValid(visual))
			visual.QueueFree();
		definition.ClearVisual();
	}

	private static Color GetTeamDetectionColor(Enums.UnitTeam team)
	{
		return team switch
		{
			Enums.UnitTeam.Player => new Color(0.2f, 0.75f, 1.0f, 0.20f),
			Enums.UnitTeam.Enemy => new Color(1.0f, 0.25f, 0.2f, 0.20f),
			_ => new Color(1.0f, 0.85f, 0.25f, 0.20f)
		};
	}

	public Godot.Collections.Dictionary<Enums.UnitTeam, GlobeTeamHolder> GetAllTeamData() => teamData;

	public GlobeTeamHolder GetTeamData(Enums.UnitTeam team) => teamData.GetValueOrDefault(team, null);

	public ResearchDatabase GetResearchDatabase() => researchDatabase;
	
	public void SetSendCraftMode(bool value, GlobeTeamHolder teamHolder, Craft craft)
	{
		SendCraftMode = value;
		teamHolder.SetSelectedCraft(craft);
		Input.SetDefaultCursorShape(
			value ? Input.CursorShape.Cross : Input.CursorShape.Arrow
		);
	}
	
	public override void Deinitialize()
	{
		// Craft data persists between scenes, but its visual and travel tween are
		// owned by this globe scene. Cancel the tween before the visual is freed and
		// never leave a disposed Godot object stored on the persistent resource.
		if (teamData != null)
		{
			foreach (GlobeTeamHolder holder in teamData.Values)
			{
				if (holder?.Bases == null) continue;
				foreach (TeamBaseCellDefinition baseDefinition in holder.Bases)
				{
					if (baseDefinition == null) continue;
					foreach (Craft craft in baseDefinition.CraftList)
					{
						if (craft == null) continue;
						craft.CancelActiveTravel();
						MeshInstance3D visual = craft.GetVisual();
						if (visual != null && GodotObject.IsInstanceValid(visual))
							visual.QueueFree();
						craft.SetVisual(null);
					}
				}
			}
		}

		if (_timeSignalsConnected && GlobeTimeManager.Instance != null)
		{
			GlobeTimeManager.Instance.DayChanged -= OnDayChanged;
			GlobeTimeManager.Instance.MonthChanged -= OnMonthChanged;
		}
		foreach (HexCellDefinition definition in _registeredDefinitions)
		{
			definition.VisibilityChanged -= OnDefinitionVisibilityChanged;
			if (definition is TeamBaseCellDefinition teamBase)
				teamBase.FacilityEffectsChanged -= OnBaseFacilityEffectsChanged;
		}
		_registeredDefinitions.Clear();
		_timeSignalsConnected = false;
	}
	
	#endregion
}
