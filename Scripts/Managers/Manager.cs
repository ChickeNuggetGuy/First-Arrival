
using Godot;

namespace FirstArrival.Scripts.Managers;
public abstract partial class Manager<T> : ManagerBase where T : ManagerBase, new()
{
	public static T Instance { get; private set; }

	[Export] public bool IsBusy;

	/// <summary>
	/// If another instance of this node exists in a newly loaded scne transfer data from old instance to new instance
	/// then destroy old instance.
	/// </summary>
	[Export] public bool passData = true;
	[Export] public bool overridePreviousInstance = true;

	public override void _Ready()
	{
		if (Instance != null)
		{
			if (Instance != this)
			{
				if (overridePreviousInstance)
				{
					Manager<T> Oldinstance = Instance as Manager<T>;
					//This is a new instance!
					if (Oldinstance != null && Oldinstance.passData)
					{
						GetInstanceData(Oldinstance.SetInstanceData());
					}
					else
					{
						return;
					}
					
					Instance = this as T;
				}
				else 
				{
					this.QueueFree();
				}
		
			
			}
		}
		else
		{
			Instance = this as T;
		}
	}
	
	public void SetIsBusy(bool isBusy)
	{
		IsBusy = isBusy;
		GD.Print($"{Name}: SetIsBusy: {isBusy} ");
	}

	protected abstract void GetInstanceData(ManagerData data);
	
	public abstract ManagerData SetInstanceData();
}