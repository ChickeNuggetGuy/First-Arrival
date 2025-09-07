
using Godot;

namespace FirstArrival.Scripts.Managers;
public abstract partial class Manager<T> : ManagerBase where T : ManagerBase, new()
{
	public static T Instance { get; private set; }



	public override void _Ready()
	{
		Instance = this as T;
	}
}