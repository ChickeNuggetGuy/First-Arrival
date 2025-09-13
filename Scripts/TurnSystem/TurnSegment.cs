using System.Threading.Tasks;
using Godot;

namespace FirstArrival.Scripts.TurnSystem;

[GlobalClass]
public abstract partial class TurnSegment: Resource
{
	public Turn parentTurn { get;protected set; }

	
	public async Task SetupCall(Turn parent)
	{
		parentTurn = parent;
		await _Setup();
	}

	protected abstract Task _Setup();

	public async Task ExecuteCall()
	{
		await _Execute();
	}

	protected abstract Task _Execute();
}