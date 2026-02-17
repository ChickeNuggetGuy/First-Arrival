using System.Threading.Tasks;
using Godot;

namespace FirstArrival.Scripts.Managers;

public abstract partial class ManagerBase : Node
{
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
	
	public virtual void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		if (data != null && data.Count > 0)
			HasLoadedData = true;
		else
			HasLoadedData = false;
	}
	
	public void DeinitializeCall()
	{
		Deinitialize();
	}
	
	public abstract void Deinitialize();
}