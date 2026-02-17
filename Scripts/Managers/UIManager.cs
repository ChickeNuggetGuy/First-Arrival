using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class UIManager : Manager<UIManager>
{
	List<UIWindow> _windows =  new List<UIWindow>();
	[Export] public bool BlockingInput { get; private set; } = false;
	public UIWindow CurrentWindow { get; private set; }
	[Export] private Control uiHolder;
	[Export] private LoadingSCcreenUI loadingSCcreenUI;
	#region Functions

	public void ShowLoadingScreen()
	{
		if (loadingSCcreenUI != null)
		{
			_ = loadingSCcreenUI.ShowCall();
		}
	}

	public override string GetManagerName() => "UIManager";

	
	/// <summary>
	/// Finds and loops through all children Ui Windows and adds them to Windows 
	/// </summary>
	/// <param name="loadingData"></param>
	protected override async Task _Setup(bool loadingData)
	{
		foreach (var child in uiHolder.GetChildren())
		{
			if (child is UIWindow window)
				_windows.Add(window);
		}

		EmitSignal(SignalName.SetupCompleted);
		await Task.CompletedTask;
	}

	
	/// <summary>
	///  Loops through and setup all children Ui Windows 
	/// </summary>
	/// <param name="loadingData"></param>
	protected override async Task _Execute(bool loadingData)
	{
		if (_windows.Count == 0) return;

		foreach (var window in _windows)
		{
			await window.SetupCall();
		}
		
		EmitSignal(SignalName.ExecuteCompleted);
		await Task.CompletedTask;
	}

	
	
	/// <summary>
	/// Block all Game inputs apart from UI Inputs
	/// </summary>
	/// <param name="blockingWindow"></param>
	/// <returns></returns>
	public bool BlockInputs( UIWindow blockingWindow)
	{
		if (BlockingInput) return false;
		
		CurrentWindow = blockingWindow;
		BlockingInput = true;
		return true;
	}
	
	
	
	public bool UnblockInputs( UIWindow blockingWindow)
	{
		if (BlockingInput && CurrentWindow != blockingWindow) return false;
		
		CurrentWindow = null;
		BlockingInput = false;
		return true;
	}
	
	#region manager Data
	public override void Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		base.Load(data);
		if(!HasLoadedData) return;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		return null;
	}
	#endregion

	public override void Deinitialize()
	{
		return;
	}

	#endregion

}
