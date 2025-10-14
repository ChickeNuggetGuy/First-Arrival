using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.TurnSystem;

[GlobalClass]
public partial class Turn : Resource
{
	[Export] public bool repeatable = true;
	public int timesExectuted = 0;
	[Export] public Enums.UnitTeam team {get; private set; }= Enums.UnitTeam.None;
	[Export(PropertyHint.ResourceType,"TurnSegment")] private TurnSegment[] turnSegments;
	
	
	public async Task SetupCall()
	{
		await _Setup();
	}

	protected virtual async Task _Setup()
	{
		if (turnSegments == null || turnSegments.Length == 0)
		{
			return;
		}
		foreach (TurnSegment turnSegment in turnSegments)
		{
			await turnSegment.SetupCall(this);
		}
	}

	public async Task ExecuteCall()
	{
		await _Execute();
		timesExectuted++;
	}

	protected virtual async Task _Execute()
	{
		if (turnSegments == null || turnSegments.Length == 0)
		{
			return;
		}
		foreach (TurnSegment turnSegment in turnSegments)
		{
			if (turnSegment == null)
			{
				continue;
			}
			await turnSegment.ExecuteCall();
		}
		return;
	}

	public virtual void onTurnEnd()
	{
		
	}
}