using System.Threading.Tasks;
using Godot;

namespace FirstArrival.Scripts.Managers;

public abstract partial class ManagerBase : Node
{
	[Export] protected bool DebugMode = false;
	public bool SetupComplete { get; protected set; }
	public bool ExecuteComplete { get; protected set; }

	[Signal]
	public delegate void SetupCompletedEventHandler();

	[Signal]
	public delegate void ExecuteCompletedEventHandler();



	public abstract string GetManagerName();

	public async Task SetupCall()
	{
		await _Setup();
		EmitSignal("SetupCompleted");
		SetupComplete = true;
	}

	protected abstract Task _Setup();

	public async Task ExecuteCall()
	{
		await _Execute();
		EmitSignal("ExecuteCompleted");
		ExecuteComplete = true;
	}

	protected abstract Task _Execute();

	public abstract Godot.Collections.Dictionary<string,Variant> Save();
	public abstract void Load(Godot.Collections.Dictionary<string,Variant> data);
}