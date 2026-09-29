using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;

public partial class HexCellDefinition : IContextUserBase
{
	public string definitionName;
	public int cellIndex;

	/// <summary>
	/// Hidden definitions keep their gameplay data but do not need a scene node.
	/// Visibility is tracked per team so revealing an enemy base does not reveal it
	/// to every team in the game.
	/// </summary>
	public bool StartsHidden { get; private set; }
	private Enums.UnitTeam _revealedToTeams = Enums.UnitTeam.None;
	private Enums.UnitTeam _visualViewingTeam = Enums.UnitTeam.Player;

	public CellDefinitionVisual Visual { get; private set; }
	public event Action<HexCellDefinition> VisibilityChanged;

	public HexCellDefinition(int cellIndex, string name, bool startsHidden = false)
	{
		this.cellIndex = cellIndex;
		this.definitionName = name;
		StartsHidden = startsHidden;
	}

	/// <summary>
	/// Actions shared by every defined globe cell. More specialized definitions
	/// extend this list with their own gameplay actions.
	/// </summary>
	public virtual Dictionary<string, Callable> GetContextActions()
	{
		var actions = new Dictionary<string, Callable>();
		if (cellIndex >= 0)
		{
			int targetCellIndex = cellIndex;
			actions.Add("Focus", Callable.From(() =>
			{
				if (OrbitalCamera.Instance != null &&
				    GlobeHexGridManager.Instance?.GetCellFromIndex(targetCellIndex) != null)
				{
					_ = OrbitalCamera.Instance.FocusOnCell(targetCellIndex);
					TutorialManager.Instance?.ReportControlAction(TutorialControlAction.FocusCell);
				}
			}));
			GlobeTeamManager teamManager = GlobeTeamManager.Instance;
			if (teamManager?.HasAvailableCraft(Enums.UnitTeam.Player) == true)
			{
				actions.Add("Send Craft There", Callable.From(() =>
				{
					UIManager.Instance?.GetWindow<SelectCraftUI>()?.ShowForDestination(
						targetCellIndex);
				}));
			}
		}

		return actions;
	}

	public bool IsVisibleTo(Enums.UnitTeam team)
		=> !StartsHidden || (_revealedToTeams & team) != 0;

	public bool RevealForTeam(Enums.UnitTeam team)
	{
		if (team == Enums.UnitTeam.None || IsVisibleTo(team)) return false;
		_revealedToTeams |= team;
		RefreshBoundVisual();
		VisibilityChanged?.Invoke(this);
		return true;
	}

	public void RevealToAllTeams()
	{
		StartsHidden = false;
		_revealedToTeams = Enums.UnitTeam.All;
		RefreshBoundVisual();
		VisibilityChanged?.Invoke(this);
	}

	public void SetStartsHidden(bool hidden)
	{
		if (StartsHidden == hidden) return;
		StartsHidden = hidden;
		RefreshBoundVisual();
		VisibilityChanged?.Invoke(this);
	}

	public void BindVisual(
		CellDefinitionVisual visual,
		Enums.UnitTeam viewingTeam = Enums.UnitTeam.Player)
	{
		Visual = visual;
		_visualViewingTeam = viewingTeam;
		if (visual != null)
		{
			visual.BindDefinition(this);
			RefreshBoundVisual();
		}
	}

	private void RefreshBoundVisual()
	{
		if (Visual != null && GodotObject.IsInstanceValid(Visual))
			Visual.SetDefinitionVisible(IsVisibleTo(_visualViewingTeam));
	}

	public void ClearVisual(CellDefinitionVisual visual = null)
	{
		if (visual == null || Visual == visual)
			Visual = null;
	}

	public virtual Godot.Collections.Dictionary<string, Variant> Save()
	{
		Godot.Collections.Dictionary<string, Variant> returnData = new();

		returnData.Add("cellIndex", cellIndex);
		returnData.Add("definitionName", definitionName);
		returnData.Add("startsHidden", StartsHidden);
		returnData.Add("revealedToTeams", (int)_revealedToTeams);
		return returnData;
	}

	public virtual void Load(
		Godot.Collections.Dictionary<string, Variant> data
	)
	{
		if (data.ContainsKey("cellIndex"))
		{
			cellIndex = data["cellIndex"].AsInt32();
		}

		if (data.ContainsKey("definitionName"))
		{
			definitionName = data["definitionName"].AsString();
		}

		if (data.ContainsKey("startsHidden"))
			StartsHidden = data["startsHidden"].AsBool();

		if (data.ContainsKey("revealedToTeams"))
			_revealedToTeams = (Enums.UnitTeam)data["revealedToTeams"].AsInt32();
	}
	
}
