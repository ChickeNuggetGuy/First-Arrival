using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

namespace FirstArrival.Scripts.Managers;

/// <summary>
/// Handles mouse input while in the GlobeScene: resolves which HexCellData
/// is under the cursor on the globe mesh and keeps the world mouse marker in sync.
/// </summary>
[GlobalClass]
public partial class GlobeInputManager : Manager<GlobeInputManager>
{
	public HexCellData? CurrentCell { get; private set; }
	public event Action<HexCellData?> CurrentCellChanged;
	private readonly Dictionary<int, HashSet<int>> _hoverRangeCache = new();

	[Export] public Camera3D camera3D;
	[Export] private Node3D mouseMarker;
	[Export] public CollisionObject3D globeMesh;

	public bool MouseOverUI => GetViewport()?.GuiGetHoveredControl() != null;

	public override string GetManagerName() => "GlobeInputManager";

	protected override Task _Setup(bool loadingData)
	{
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		return Task.CompletedTask;
	}

	public override void _Process(double delta)
	{
		if (UIManager.Instance.BlockingInput)
		{
			return;
		}

		if (!ExecuteComplete)
		{
			return;
		}

		if (GameManager.Instance.currentScene != GameManager.GameScene.GlobeScene)
		{
			return;
		}

		UpdateHexCellUnderMouse();
		base._Process(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		return;
	}

	private void UpdateHexCellUnderMouse()
	{
		if (MouseOverUI)
		{
			ClearCurrentCell();
			return;
		}

		Vector3? mousePos = GetMouseGlobePosition();

		if (mousePos != null)
		{
			HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromPosition(mousePos.Value);

			if (cell != null)
			{
				SetCurrentCell(cell.Value);
				GlobeHexGridManager.Instance.SetDebugHighlightedCountryFromIndex(cell.Value.Index);

				if (mouseMarker != null)
					mouseMarker.GlobalPosition = cell.Value.Center;

				return;
			}
		}

		ClearCurrentCell();
	}

	private void SetCurrentCell(HexCellData cell)
	{
		if (CurrentCell.HasValue && CurrentCell.Value.Index == cell.Index)
		{
			CurrentCell = cell;
			return;
		}

		CurrentCell = cell;
		_hoverRangeCache.Clear();
		CurrentCellChanged?.Invoke(CurrentCell);
	}

	private void ClearCurrentCell()
	{
		if (!CurrentCell.HasValue) return;

		CurrentCell = null;
		_hoverRangeCache.Clear();
		CurrentCellChanged?.Invoke(null);
		GlobeHexGridManager.Instance?.SetDebugHighlightedCountryFromIndex(-1);

		if (mouseMarker != null)
			mouseMarker.GlobalPosition = new Vector3(-1, -1, -1);
	}

	/// <summary>
	/// Returns whether a cell is within a hex-step radius of the hovered cell.
	/// Results are cached per radius and recalculated only when the hover target
	/// changes, allowing many cell labels to share the same range lookup.
	/// </summary>
	public bool IsCellNearCurrentCell(int cellIndex, int rangeSteps)
	{
		if (!CurrentCell.HasValue || cellIndex < 0 || rangeSteps < 0) return false;

		if (!_hoverRangeCache.TryGetValue(rangeSteps, out HashSet<int> nearbyCells))
		{
			nearbyCells = new HashSet<int>();
			GlobeHexGridManager grid = GlobeHexGridManager.Instance;
			if (grid == null) return false;

			foreach (HexCellData cell in grid.GetCellsInStepRange(
				CurrentCell.Value,
				rangeSteps))
			{
				nearbyCells.Add(cell.Index);
			}

			_hoverRangeCache.Add(rangeSteps, nearbyCells);
		}

		return nearbyCells.Contains(cellIndex);
	}

	public Vector3? GetMouseGlobePosition()
	{
		var spaceState = GetTree().Root.GetWorld3D().DirectSpaceState;

		var cam = camera3D;
		var mousePos = GetViewport().GetMousePosition();

		var origin = cam.ProjectRayOrigin(mousePos);
		var end = origin + cam.ProjectRayNormal(mousePos) * 400;
		var query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollideWithAreas = true;

		var result = spaceState.IntersectRay(query);

		if (result.Count == 0)
		{
			return null;
		}
		else if (result["collider"].AsGodotObject() == globeMesh)
		{
			return result["position"].AsVector3();
		}

		return null;
	}

	public Vector2 GetLatLonFromPosition(Vector3 position)
	{
		float radius = position.Length();

		if (radius == 0) return Vector2.Zero;

		float latitude = Mathf.Asin(position.Y / radius);
		float longitude = Mathf.Atan2(position.X, position.Z);

		return new Vector2(Mathf.RadToDeg(latitude), Mathf.RadToDeg(longitude));
	}

	#region manager Data

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (!HasLoadedData) return Task.CompletedTask;
		return Task.CompletedTask;
	}

	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		return null;
	}

	#endregion

	public override void Deinitialize()
	{
		return;
	}
}
