using System.Threading.Tasks;
using Godot;

namespace FirstArrival.Scripts.Managers;

public abstract partial class ManagerBase : Node
{
	
	// Inside ManagerBase.cs
	public bool HasInitialized { get; set; } = false;

	// If this is an Autoload, we usually want to run Setup/Execute only once at game start.
	[Export] public bool ShouldExecuteOnlyOnce { get; set; } = false;
	
	[Export] protected bool DebugMode = false;
	[Export] public bool includeInLoadingCalculation = true;
	public bool SetupComplete { get; protected set; }
	public bool ExecuteComplete { get; protected set; }
	
	public bool HasLoadedData { get; protected set; }

	[Signal] public delegate void SetupCompletedEventHandler();
	[Signal] public delegate void ExecuteCompletedEventHandler();

	public abstract string GetManagerName();

	public async Task SetupCall(bool loadingData)
	{
		await _Setup(loadingData);
		EmitSignal(SignalName.SetupCompleted);
		SetupComplete = true;
	}

	protected abstract Task _Setup(bool loadingData);

	public async Task ExecuteCall(bool loadingData)
	{
		await _Execute(loadingData);
		EmitSignal("ExecuteCompleted");
		ExecuteComplete = true;
	}

	protected abstract Task _Execute(bool loadingData);

	public abstract Godot.Collections.Dictionary<string, Variant> Save();
	
	public virtual async Task LoadCall(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data is { Count: > 0 })
			HasLoadedData = true;
		else
			HasLoadedData = false;

		await Load(data);
	}

	public abstract Task Load(Godot.Collections.Dictionary<string, Variant> data);
	
	public abstract void Deinitialize();
}