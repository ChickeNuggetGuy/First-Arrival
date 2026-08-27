using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.UI;

namespace FirstArrival.Scripts.Managers;
[GlobalClass]
public partial class UIManager : Manager<UIManager>
{
	List<UIWindow> _windows =  new List<UIWindow>();
	[Export] public bool BlockingInput { get; private set; } = false;
	public UIWindow CurrentWindow { get; private set; }
	[Export] private Control uiHolder;
	[Export] private LoadingScreenUI loadingSCcreenUI;
	
	[Export]public MouseHeldInventoryUI  mouseHeldInventoryUI {get; protected set;}
	private Godot.Collections.Dictionary<string, Variant>
		_loadedMouseHeldInventory;
	#region Functions

	public void ShowLoadingScreen()
	{
		if (loadingSCcreenUI != null)
		{
			_ = loadingSCcreenUI.ShowCall();
		}
	}

	public async Task HideLoadingScreen()
	{
		if (loadingSCcreenUI != null &&
		    GodotObject.IsInstanceValid(loadingSCcreenUI))
		{
			await loadingSCcreenUI.HideCall(false);
		}
	}

	public override string GetManagerName() => "UIManager";

	public override void _Ready()
	{
		base._Ready();
		GatherWindowsAndApplyInitialVisibility();
	}

	
	/// <summary>
	/// Finds and loops through all children Ui Windows and adds them to Windows 
	/// </summary>
	/// <param name="loadingData"></param>
	protected override async Task _Setup(bool loadingData)
	{
		GatherWindowsAndApplyInitialVisibility();

		EmitSignal(SignalName.SetupCompleted);
		await Task.CompletedTask;
	}

	private void GatherWindowsAndApplyInitialVisibility()
	{
		_windows.Clear();
		if (uiHolder == null)
		{
			GD.PushError("UIManager cannot initialize windows because uiHolder is not assigned.");
			return;
		}

		foreach (var child in uiHolder.GetChildren())
		{
			if (child is UIWindow window)
			{
				_windows.Add(window);
				window.ApplyInitialVisibility();
			}
		}
	}

	
	/// <summary>
	///  Loops through and setup all children Ui Windows 
	/// </summary>
	/// <param name="loadingData"></param>
	protected override async Task _Execute(bool loadingData)
	{
		if (_windows.Count == 0)
		{
			GD.Print("No UI windows found");
			return;
		}

		foreach (var window in _windows)
		{
			await window.SetupCall();
		}

		if (_loadedMouseHeldInventory != null &&
		    mouseHeldInventoryUI?.InventoryGrid != null)
		{
			mouseHeldInventoryUI.previousInventory = null;
			mouseHeldInventoryUI.InventoryGrid.LoadContents(
				_loadedMouseHeldInventory);
			_loadedMouseHeldInventory = null;
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

	/// <summary>Returns the first registered window of the requested type.</summary>
	public T GetWindow<T>() where T : UIWindow
	{
		foreach (UIWindow window in _windows)
		{
			if (window is T typedWindow && GodotObject.IsInstanceValid(typedWindow))
				return typedWindow;
		}

		return null;
	}
	
	#region manager Data
	public override Task Load(Godot.Collections.Dictionary<string,Variant> data)
	{
		_loadedMouseHeldInventory = null;
		if (data != null && data.TryGetValue(
			    "mouseHeldInventory",
			    out Variant inventoryValue) &&
		    inventoryValue.VariantType == Variant.Type.Dictionary)
		{
			_loadedMouseHeldInventory = inventoryValue
				.AsGodotDictionary<string, Variant>();
		}
		return Task.CompletedTask;
	}

	public override Godot.Collections.Dictionary<string,Variant> Save()
	{
		var data = new Godot.Collections.Dictionary<string, Variant>();
		mouseHeldInventoryUI?.TryReturnHeldItem();
		if (mouseHeldInventoryUI?.InventoryGrid?.UniqueItems.Count > 0)
		{
			data["mouseHeldInventory"] =
				mouseHeldInventoryUI.InventoryGrid.SaveContents();
		}
		return data;
	}
	#endregion

	public override void Deinitialize()
	{
		return;
	}

	#endregion

}
