
using Godot;

namespace FirstArrival.Scripts.Managers;
public abstract partial class Manager<T> : ManagerBase where T : ManagerBase, new()
{
	public static T Instance { get; private set; }

	public bool IsBusy { get; private set; }

	public override void _Ready()
	{
		Instance = this as T;
	}
	
	public void SetIsBusy(bool isBusy)
	{
		IsBusy = isBusy;
		GD.Print($"{Name}: SetIsBusy: {isBusy} ");
	}
}