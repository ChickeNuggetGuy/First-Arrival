using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

[GlobalClass]
public partial class GlobeTeamManager : Manager<GlobeTeamManager>
{
	private PackedScene baseScene;
	private Node baseContainer;
	
	public PackedScene shipScene;
	public Node shipContainer;
	
	public bool buildBaseMode = false;
	public bool buyCraftMode = false;
	private bool _sendCraftMode = false;
	private readonly HashSet<HexCellDefinition> _registeredDefinitions = new();
	private readonly System.Collections.Generic.Dictionary<TeamBaseCellDefinition, TeambasedVisual> _baseVisuals = new();

	[Export] public Enums.UnitTeam ViewingTeam { get; set; } = Enums.UnitTeam.Player;
	[Export] private bool scanForDefinitionsDaily = true;
	private bool _timeSignalsConnected;

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
		teamsConfig = Enums.UnitTeam.Player | Enums.UnitTeam.Enemy;
		ShouldExecuteOnlyOnce = true;
		base._Ready();
	}

	protected override async Task _Setup(bool loadingData)
	{
		teamData ??= new Godot.Collections.Dictionary<Enums.UnitTeam, GlobeTeamHolder>();
		
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

		if (scanForDefinitionsDaily && GlobeTimeManager.Instance != null && !_timeSignalsConnected)
		{
			GlobeTimeManager.Instance.DayChanged += OnDayChanged;
			_timeSignalsConnected = true;
		}
		await Task.CompletedTask;
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

		MeshInstance3D shipNode = craft.visual ?? shipScene.Instantiate<MeshInstance3D>();

		if (craft.visual == null)
		{
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

		return new Godot.Collections.Dictionary<string, Variant> { ["teamData"] = teamHolderData };
	}

	public override async Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!HasLoadedData) return;

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
					AddChild(newTeam);
					teamData.Add(teamType, newTeam);
				}

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

		if (buyCraftMode)
		{
			if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed &&
			    mouseButton.ButtonIndex == MouseButton.Left)
			{
				HexCellData? cell = GlobeInputManager.Instance.CurrentCell;

				if (cell == null) return;
				
				if (!teamData[Enums.UnitTeam.Player].TryGetBaseAtIndex(cell.Value.Index, out var baseCell)) return;
				
				if(baseCell.TryAddCraft(Enums.CraftStatus.Home, (Craft)testCraft.Duplicate(true)))
				{
					GD.Print("Adding craft");
					buyCraftMode = false;
				}
				else
				{
					GD.Print("Adding Craft failed");
				}
			}
		}

		if (_sendCraftMode)
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
			teamData.Add(team, new GlobeTeamHolder(team, new List<TeamBaseCellDefinition>()));
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
	}

	public void UnregisterCellDefinition(HexCellDefinition definition)
	{
		if (definition == null || !_registeredDefinitions.Remove(definition)) return;
		definition.VisibilityChanged -= OnDefinitionVisibilityChanged;
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

	private void OnDayChanged(int dayOfYear, int dayOfMonth, Enums.Day day)
	{
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
	
	public void SetSendCraftMode(bool value, GlobeTeamHolder teamHolder, Craft craft)
	{
		_sendCraftMode = value;
		teamHolder.SetSelectedCraft(craft);
		Input.SetDefaultCursorShape(
			value ? Input.CursorShape.Cross : Input.CursorShape.Arrow
		);
	}
	
	public override void Deinitialize()
	{
		if (_timeSignalsConnected && GlobeTimeManager.Instance != null)
			GlobeTimeManager.Instance.DayChanged -= OnDayChanged;
		_timeSignalsConnected = false;
	}
	
	#endregion
}
