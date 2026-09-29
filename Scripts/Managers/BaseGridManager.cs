using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot.Collections;

[GlobalClass]
public partial class BaseGridManager : Manager<BaseGridManager>
{
	private const int SIZEX = 10;
	private const int SIZEZ = 10;
	private const int CELLSIZE = 20;
	private const float CELLSPACING = CELLSIZE;
	
	private BaseCell[,] cells;
	private PackedScene selectedFacilityScene;
	private FacilityDefinition selectedFacilityDefinition;
	private BaseCell placementGhost;
	private readonly System.Collections.Generic.List<GeometryInstance3D> placementGhostVisuals = new();
	private StandardMaterial3D validGhostMaterial;
	private StandardMaterial3D invalidGhostMaterial;
	private bool previewPlacementIsValid;
	private TeamBaseCellDefinition CurrentBase => GameManager.Instance?.currentBase;

	private BaseCell[] cellsArray
	{
		get
		{
			BaseCell[] allCells = new BaseCell[cells.Length];

			for (int i = 0; i < cells.GetLength(0); i++)
			{
				for (int j = 0; j < cells.GetLength(1); j++)
				{
					allCells[j + i * cells.GetLength(1)] = cells[i, j];
				}
			}

			return allCells;
		}
	}
	
	[Export] private Dictionary<String, PackedScene> cellScenes = new();
	[Export] private Dictionary<String, PackedScene> facilityScenes = new();
	[Export] public bool BuildFacilityMode { get; private set; } = false;

	[Signal]
	public delegate void FacilityConstructedEventHandler(
		BaseCell cell,
		string facilityName,
		Node3D facility);
	[Signal]
	public delegate void FacilityConstructionStartedEventHandler(
		BaseCell cell,
		string facilityName,
		int remainingBuildDays);

	public override string GetManagerName() => "BaseGridManager";

	protected override async Task _Setup(bool loadingData)
	{
		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		CreateGrid();
		if (CurrentBase != null)
			CurrentBase.FacilityCompleted += OnFacilityCompleted;
		await Task.CompletedTask;
	}


	private void CreateGrid()
	{
		cells = new BaseCell[SIZEX, SIZEZ];
		for (int x = 0; x < SIZEX; x++)
		{
			for (int z = 0; z < SIZEZ; z++)
			{
				cells[x,z] = CreateBaseCell(x,z);
			}
		}

		RestoreFacilityVisuals();
	}

	private BaseCell CreateBaseCell(int x, int z)
	{
		Vector3 pos = new Vector3(x * CELLSPACING, 0, z * CELLSPACING);

		BaseCell cell = (BaseCell)cellScenes["test"].Instantiate();
		cell.Position = pos;
		cell.gridCoords = new Vector2(x, z);
		cell.worldPosition = pos;
		cell.meshInstance = cell.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		AddChild(cell);
		return cell;
	}

	public BaseCell GetCellFromMouse()
	{
		return GetCellFromScreenPosition(GetViewport().GetMousePosition());
	}

	private BaseCell GetCellFromScreenPosition(Vector2 screenPosition)
	{
		if (cells == null)
			return null;

		Camera3D camera = GetViewport().GetCamera3D();
		if (camera == null || !GodotObject.IsInstanceValid(camera))
			camera = BaseCamera.Instance;
		if (camera == null || !GodotObject.IsInstanceValid(camera))
			return null;

		Vector3 rayOrigin = camera.ProjectRayOrigin(screenPosition);
		Vector3 rayDirection = camera.ProjectRayNormal(screenPosition);

		if (Mathf.IsZeroApprox(rayDirection.Y))
			return null;

		float gridY = cells[0, 0]?.GlobalPosition.Y ?? 0.0f;
		float distance = (gridY - rayOrigin.Y) / rayDirection.Y;
		if (distance < 0.0f)
			return null;

		Vector3 hitPosition = rayOrigin + rayDirection * distance;
		BaseCell closestCell = null;
		float closestDistanceSquared = float.MaxValue;

		foreach (BaseCell cell in cells)
		{
			if (cell == null || !GodotObject.IsInstanceValid(cell))
				continue;

			Vector2 offset = new Vector2(
				hitPosition.X - cell.GlobalPosition.X,
				hitPosition.Z - cell.GlobalPosition.Z);
			float distanceSquared = offset.LengthSquared();

			if (distanceSquared < closestDistanceSquared)
			{
				closestCell = cell;
				closestDistanceSquared = distanceSquared;
			}
		}

		float halfSpacing = CELLSPACING * 0.5f;
		if (closestCell == null ||
			Mathf.Abs(hitPosition.X - closestCell.GlobalPosition.X) > halfSpacing ||
			Mathf.Abs(hitPosition.Z - closestCell.GlobalPosition.Z) > halfSpacing)
		{
			return null;
		}

		return closestCell;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!BuildFacilityMode)
			return;

		if (@event is InputEventKey
			{
				Pressed: true,
				Echo: false,
				Keycode: Key.Escape
			})
		{
			SetBuildFacilityMode(false);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
			return;

		if (mouseEvent.ButtonIndex == MouseButton.Right)
		{
			SetBuildFacilityMode(false);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseEvent.ButtonIndex != MouseButton.Left)
			return;

		if (selectedFacilityScene == null)
		{
			SetBuildFacilityMode(false);
			return;
		}

		BaseCell cell = GetCellFromScreenPosition(mouseEvent.Position);
		UpdatePlacementPreview(cell);
		if (cell != null && previewPlacementIsValid)
			PlaceSelectedFacility(cell);

		GetViewport().SetInputAsHandled();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!BuildFacilityMode || selectedFacilityScene == null)
			return;

		UpdatePlacementPreview(GetCellFromMouse());
	}

	public override void Deinitialize()
	{
		SetBuildFacilityMode(false);
		if (CurrentBase != null)
			CurrentBase.FacilityCompleted -= OnFacilityCompleted;
	}

	public void SetBuildFacilityMode(bool value)
	{
		// The main UI becomes interactive during _Ready so the player always has
		// an escape route. Ignore facility input until this manager has built its
		// grid during the later execute phase.
		if (value && cells == null)
			return;

		BuildFacilityMode = value;

		if (BuildFacilityMode)
		{
			ClearPlacementSelection();
			BaseCamera camera = BaseCamera.Instance;
			if (camera != null && GodotObject.IsInstanceValid(camera))
				camera.FocusOn(cellsArray);
		}
		else
		{
			ClearPlacementSelection();
		}
	}

	public sealed class FacilityBuildOption
	{
		public string FacilityKey { get; }
		public FacilityDefinition Definition { get; }
		public bool CanAfford { get; }

		public FacilityBuildOption(
			string facilityKey,
			FacilityDefinition definition,
			bool canAfford)
		{
			FacilityKey = facilityKey;
			Definition = definition;
			CanAfford = canAfford;
		}
	}

	/// <summary>
	/// Returns the facilities that may be added to the current base. The UI owns
	/// how these options are displayed; this manager remains responsible for
	/// validating affordability and placement when an option is chosen.
	/// </summary>
	public System.Collections.Generic.IReadOnlyList<FacilityBuildOption>
		GetAvailableFacilityOptions()
	{
		var options =
			new System.Collections.Generic.List<FacilityBuildOption>();
		foreach (String facilityName in facilityScenes.Keys)
		{
			if (string.IsNullOrWhiteSpace(facilityName)) continue;
			if (!facilityScenes.TryGetValue(facilityName, out PackedScene scene) ||
			    scene == null ||
			    !TryCreateFacilityCell(scene, out BaseCell facilityCell))
			{
				continue;
			}

			FacilityDefinition definition = facilityCell.FacilityDefinition;
			bool selectable = IsFacilitySelectable(definition);
			facilityCell.Free();
			if (!selectable) continue;

			options.Add(new FacilityBuildOption(
				facilityName,
				definition,
				CanAffordFacility(definition)));
		}

		options.Sort((left, right) => string.Compare(
			left.Definition.DisplayName,
			right.Definition.DisplayName,
			StringComparison.OrdinalIgnoreCase));
		return options;
	}

	private bool IsFacilitySelectable(FacilityDefinition definition)
	{
		if (definition == null || CurrentBase == null)
			return false;
		if (!definition.UniquePerBase)
			return true;

		foreach (FacilityConstruction existing in CurrentBase.Facilities)
		{
			if (existing.FacilityId.Equals(
				definition.FacilityId,
				StringComparison.OrdinalIgnoreCase))
				return false;
		}
		return true;
	}

	private bool CanAffordFacility(FacilityDefinition definition) =>
		definition != null &&
		GameManager.Instance != null &&
		GameManager.Instance.currentBaseFunds >= Mathf.Max(0, definition.InitialCost);

	public bool BeginFacilityPlacement(string facilityKey)
	{
		if (cells == null ||
		    string.IsNullOrWhiteSpace(facilityKey) ||
		    !facilityScenes.TryGetValue(facilityKey, out PackedScene facilityScene) ||
		    facilityScene == null ||
		    !TryCreateFacilityCell(facilityScene, out BaseCell validationCell))
		{
			return false;
		}

		FacilityDefinition definition = validationCell.FacilityDefinition;
		validationCell.Free();
		if (!IsFacilitySelectable(definition) || !CanAffordFacility(definition))
			return false;

		SetBuildFacilityMode(true);

		if (!TryCreateFacilityCell(facilityScene, out BaseCell ghost))
		{
			SetBuildFacilityMode(false);
			return false;
		}

		selectedFacilityScene = facilityScene;
		selectedFacilityDefinition = ghost.FacilityDefinition;
		placementGhost = ghost;
		placementGhost.Name = $"{selectedFacilityDefinition.DisplayName} Placement Preview";
		placementGhost.ProcessMode = ProcessModeEnum.Disabled;
		AddChild(placementGhost);

		placementGhostVisuals.Clear();
		foreach (Node child in placementGhost.FindChildren(
			"*",
			nameof(GeometryInstance3D),
			true,
			false))
		{
			if (child is not GeometryInstance3D visual) continue;
			visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			placementGhostVisuals.Add(visual);
		}

		validGhostMaterial ??= CreateGhostMaterial(
			new Color(0.15f, 1.0f, 0.35f, 0.48f));
		invalidGhostMaterial ??= CreateGhostMaterial(
			new Color(1.0f, 0.16f, 0.12f, 0.48f));
		UpdatePlacementPreview(GetCellFromMouse());
		return true;
	}

	private static StandardMaterial3D CreateGhostMaterial(Color color) => new()
	{
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		AlbedoColor = color,
		NoDepthTest = true
	};

	private void UpdatePlacementPreview(BaseCell cell)
	{
		if (placementGhost == null || !GodotObject.IsInstanceValid(placementGhost))
			return;

		bool hasCell = cell != null && GodotObject.IsInstanceValid(cell);
		previewPlacementIsValid = hasCell &&
			CanAffordFacility(selectedFacilityDefinition) &&
			CanPlaceFacility(
			new Vector2I(
				Mathf.RoundToInt(cell.gridCoords.X),
				Mathf.RoundToInt(cell.gridCoords.Y)),
			selectedFacilityDefinition,
			out _);

		if (hasCell)
			placementGhost.Position = cell.Position + Vector3.Up * 0.15f;

		Material material = previewPlacementIsValid
			? validGhostMaterial
			: invalidGhostMaterial;
		foreach (GeometryInstance3D visual in placementGhostVisuals)
		{
			visual.Visible = hasCell;
			visual.MaterialOverride = material;
		}
	}

	private bool PlaceSelectedFacility(BaseCell selectedCell)
	{
		if (selectedCell == null ||
		    !GodotObject.IsInstanceValid(selectedCell) ||
		    selectedFacilityScene == null ||
		    selectedFacilityDefinition == null ||
		    !TryCreateFacilityCell(selectedFacilityScene, out BaseCell replacementCell))
		{
			return false;
		}

		Vector2I origin = new(
			Mathf.RoundToInt(selectedCell.gridCoords.X),
			Mathf.RoundToInt(selectedCell.gridCoords.Y));
		FacilityDefinition definition = replacementCell.FacilityDefinition;
		if (!CanPlaceFacility(
			origin,
			definition,
			out FacilityConstruction attachment))
		{
			replacementCell.Free();
			return false;
		}

		FacilityConstruction construction = FacilityConstruction.Create(
			definition,
			origin,
			selectedFacilityScene.ResourcePath,
			attachment?.Id);
		TeamBaseCellDefinition currentBase = CurrentBase;
		int initialCost = Mathf.Max(0, definition.InitialCost);
		if (currentBase == null || !TrySpendFacilityCost(initialCost))
		{
			replacementCell.Free();
			return false;
		}
		if (!currentBase.TryAddFacilityConstruction(construction))
		{
			RefundFacilityCost(initialCost);
			replacementCell.Free();
			return false;
		}

		if (!ReplaceCellWithFacility(
			selectedCell,
			replacementCell,
			construction,
			out BaseCell facilityCell))
		{
			currentBase.TryRemoveFacilityConstruction(construction);
			RefundFacilityCost(initialCost);
			replacementCell.Free();
			return false;
		}
		currentBase.RecordFacilityConstructionExpenditure(initialCost);

		EmitSignal(
			SignalName.FacilityConstructionStarted,
			facilityCell,
			construction.DisplayName,
			construction.RemainingBuildDays);
		GameManager.Instance.SyncCurrentBaseToGlobeState();
		SetBuildFacilityMode(false);
		return true;
	}

	private static bool TrySpendFacilityCost(int amount)
	{
		GameManager gameManager = GameManager.Instance;
		if (gameManager == null || amount < 0 || gameManager.currentBaseFunds < amount)
			return false;

		gameManager.currentBaseFunds -= amount;
		return true;
	}

	private static void RefundFacilityCost(int amount)
	{
		if (amount <= 0 || GameManager.Instance == null) return;
		decimal refunded = (decimal)GameManager.Instance.currentBaseFunds + amount;
		GameManager.Instance.currentBaseFunds = (long)Math.Min(refunded, long.MaxValue);
	}

	private bool ReplaceCellWithFacility(
		BaseCell cellToReplace,
		BaseCell replacementCell,
		FacilityConstruction construction,
		out BaseCell facilityCell)
	{
		facilityCell = null;

		int x = Mathf.RoundToInt(cellToReplace.gridCoords.X);
		int z = Mathf.RoundToInt(cellToReplace.gridCoords.Y);
		if (x < 0 || x >= cells.GetLength(0) ||
			z < 0 || z >= cells.GetLength(1) ||
			cells[x, z] != cellToReplace)
		{
			GD.PushError("Cannot replace the selected facility cell: its grid coordinates are invalid.");
			return false;
		}
		foreach (Vector2I occupiedCell in construction.GetOccupiedCells())
		{
			if (!IsInBounds(occupiedCell))
			{
				GD.PushError(
					$"Cannot place '{construction.DisplayName}': its footprint " +
					"extends outside the base grid.");
				return false;
			}
		}

		Vector3 position = cellToReplace.Position;
		StringName cellName = cellToReplace.Name;

		RemoveChild(cellToReplace);
		cellToReplace.QueueFree();

		replacementCell.Name = cellName;
		replacementCell.Position = position;
		replacementCell.gridCoords = new Vector2(x, z);
		replacementCell.worldPosition = position;
		replacementCell.meshInstance =
			replacementCell.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		replacementCell.ConfigureFacility(construction, true);

		AddChild(replacementCell);
		cells[x, z] = replacementCell;
		foreach (Vector2I occupiedCell in construction.GetOccupiedCells())
		{
			if (occupiedCell == construction.Origin) continue;
			cells[occupiedCell.X, occupiedCell.Y]
				.ConfigureFacility(construction, false);
		}
		facilityCell = replacementCell;
		return true;
	}

	private bool TryCreateFacilityCell(
		PackedScene scene,
		out BaseCell facilityCell)
	{
		facilityCell = null;
		Node instance = scene?.Instantiate();
		if (instance is not BaseCell cell || cell.FacilityDefinition == null)
		{
			GD.PushError(
				"Facility scenes must use BaseCell as their root and assign a " +
				"FacilityDefinition resource.");
			instance?.Free();
			return false;
		}

		cell.CenterFacilityVisualOnFootprint(CELLSPACING);
		facilityCell = cell;
		return true;
	}

	private bool CanPlaceFacility(
		Vector2I origin,
		FacilityDefinition definition,
		out FacilityConstruction attachment)
	{
		attachment = null;
		if (definition == null || CurrentBase == null) return false;

		if (definition.UniquePerBase)
		{
			foreach (FacilityConstruction existing in CurrentBase.Facilities)
			{
				if (existing.FacilityId.Equals(
					definition.FacilityId,
					StringComparison.OrdinalIgnoreCase))
					return false;
			}
		}

		Vector2I size = definition.GetValidatedGridSize();
		for (int x = 0; x < size.X; x++)
		{
			for (int z = 0; z < size.Y; z++)
			{
				Vector2I cell = origin + new Vector2I(x, z);
				if (!IsInBounds(cell) ||
					CurrentBase.GetFacilityAtGridCell(cell) != null)
					return false;
			}
		}

		var candidates = new System.Collections.Generic.List<FacilityConstruction>();
		var candidateIds = new System.Collections.Generic.HashSet<string>();
		Vector2I[] directions =
		{
			Vector2I.Left,
			Vector2I.Right,
			Vector2I.Up,
			Vector2I.Down
		};
		for (int x = 0; x < size.X; x++)
		{
			for (int z = 0; z < size.Y; z++)
			{
				Vector2I footprintCell = origin + new Vector2I(x, z);
				foreach (Vector2I direction in directions)
				{
					Vector2I neighbor = footprintCell + direction;
					if (!IsInBounds(neighbor)) continue;
					FacilityConstruction candidate =
						CurrentBase.GetFacilityAtGridCell(neighbor);
					if (candidate != null && candidateIds.Add(candidate.Id))
						candidates.Add(candidate);
				}
			}
		}

		if (candidates.Count == 0) return false;
		candidates.Sort((left, right) =>
		{
			if (left.IsConstructed != right.IsConstructed)
				return left.IsConstructed ? -1 : 1;
			return left.RemainingBuildDays.CompareTo(right.RemainingBuildDays);
		});
		attachment = candidates[0];
		return true;
	}

	private bool IsInBounds(Vector2I cell) =>
		cell.X >= 0 && cell.X < SIZEX && cell.Y >= 0 && cell.Y < SIZEZ;

	private void RestoreFacilityVisuals()
	{
		if (CurrentBase == null) return;

		foreach (FacilityConstruction construction in CurrentBase.Facilities)
		{
			PackedScene scene = string.IsNullOrEmpty(construction.ScenePath)
				? null
				: ResourceLoader.Load<PackedScene>(construction.ScenePath);
			if (scene == null)
			{
				facilityScenes.TryGetValue(
					construction.FacilityId,
					out scene);
			}
			if (scene == null ||
				!TryCreateFacilityCell(scene, out BaseCell replacementCell) ||
				!IsInBounds(construction.Origin))
			{
				GD.PushError(
					$"Could not restore facility visual '{construction.DisplayName}'.");
				continue;
			}

			BaseCell cellToReplace =
				cells[construction.Origin.X, construction.Origin.Y];
			if (!ReplaceCellWithFacility(
				cellToReplace,
				replacementCell,
				construction,
				out _))
			{
				replacementCell.Free();
			}
		}
	}

	private void OnFacilityCompleted(FacilityConstruction construction)
	{
		if (construction == null || !IsInBounds(construction.Origin)) return;

		BaseCell cell = cells?[construction.Origin.X, construction.Origin.Y];
		if (cell == null) return;
		foreach (Vector2I occupiedCell in construction.GetOccupiedCells())
		{
			if (IsInBounds(occupiedCell))
				cells[occupiedCell.X, occupiedCell.Y]
					.RefreshConstructionAppearance();
		}

		EmitSignal(
			SignalName.FacilityConstructed,
			cell,
			construction.DisplayName,
			cell);
		GameManager.Instance.SyncCurrentBaseToGlobeState();
	}

	private void ClearPlacementSelection()
	{
		selectedFacilityScene = null;
		selectedFacilityDefinition = null;
		previewPlacementIsValid = false;
		foreach (GeometryInstance3D visual in placementGhostVisuals)
		{
			if (visual != null && GodotObject.IsInstanceValid(visual))
				visual.Visible = false;
		}
		placementGhostVisuals.Clear();

		if (placementGhost != null && GodotObject.IsInstanceValid(placementGhost))
			placementGhost.QueueFree();
		placementGhost = null;
	}

	#region Save/Loading

	public override Dictionary<string, Variant> Save()
	{
		return new Dictionary<string, Variant>();
	}

	public override Task Load(Dictionary<string, Variant> data)
	{
		return Task.CompletedTask;
	}

	#endregion
}
