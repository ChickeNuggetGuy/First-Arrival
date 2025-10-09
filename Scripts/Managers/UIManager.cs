using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class UIManager : Manager<UIManager>
{
	List<UIWindow> _windows =  new List<UIWindow>();
	[Export] private Control uiHolder;
	protected override async Task _Setup()
	{
		foreach (var child in uiHolder.GetChildren())
		{
			if (child is UIWindow window)
				_windows.Add(window);
		}
	}

	protected override async Task _Execute()
	{
		if (_windows.Count == 0) return;

		foreach (var window in _windows)
		{
			await window.SetupCall();
		}
	}
	
	#region manager Data
	protected override void GetInstanceData(ManagerData data)
	{
		GD.Print("No data to transfer");
	}

	public override ManagerData SetInstanceData()
	{
		return null;
	}
	#endregion
}
