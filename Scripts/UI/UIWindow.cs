using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.UI.UIAnimations;

public abstract partial class UIWindow : UIElement
{
	[Export] protected Control Visual;
	[Export] Button toggleButton;
	[Export] public string uiName { get; private set; }
	[Export] protected Key toggleKey { get; private set; }
	[Export] bool startHidden = false;
	protected List<UIElement> uiElements = new List<UIElement>();
	[Export] protected bool blockInputs = false;
	public bool IsShown {get; private set;}
	
	
	#region Animation
	bool isBusy = false;
	[ExportGroup("Animations")]
	[Export(PropertyHint.ResourceType, "UIAnimation")]
	private UIAnimation showAnimation;
	[Export(PropertyHint.ResourceType, "UIAnimation")]
	private UIAnimation hideAnimation;
	

	
	#endregion
	public override async Task SetupCall()
	{
		ApplyInitialVisibility();

		// Find all UIElements that belong to this window
		uiElements = GetChildUIElements();
    
		// Now you can work with the UIElements that belong to this window
		foreach (var element in uiElements)
		{
			await element.SetupCall();
		}

		if (startHidden)
		{ 
			await HideCall(false);
		}
		else
		{
			IsShown = Visible && (Visual == null || Visual.Visible);
		}
		await base.SetupCall();
	}

	/// <summary>
	/// Applies only the scene's initial visibility contract. UIManager calls this
	/// from _Ready so hidden windows are safe before the first rendered frame.
	/// </summary>
	public void ApplyInitialVisibility()
	{
		if (startHidden)
		{
			Hide();
			Visual?.Hide();
			IsShown = false;
			MouseFilter = MouseFilterEnum.Ignore;
			return;
		}

		IsShown = Visible && (Visual == null || Visual.Visible);
		MouseFilter = IsShown
			? MouseFilterEnum.Stop
			: MouseFilterEnum.Ignore;
	}

	private void ToggleButtonOnPressed()
	{
		Toggle();
	}

	public override void _Input(InputEvent @event)
	{
		if(isBusy)return;
		base._Input(@event);
		if (@event is InputEventKey inputEventKey && inputEventKey.Keycode == toggleKey && @event.IsPressed())
		{
			Toggle();
		}
	}

	public async Task Toggle()
	{
		if (IsShown)
		{
			await HideCall();
		}
		else
		{
			await ShowCall();
		}
	}

	public async Task ShowCall(bool playAnimation = true)
	{
		// Make the window visible before drawing it. DrawUI can await other work
		// (or fail because game data is not ready yet), and neither case should
		// leave an inspector-hidden root Control permanently hidden.
		Show();
		Visual?.Show();
		IsShown = true;
		MouseFilter = MouseFilterEnum.Stop;

		await DrawUI();
		_Show();
		foreach (var uiElement in uiElements)
		{
			if (uiElement is UIWindow uiWindow)
			{
				if(uiWindow.GetParent() == this) continue;
				await uiWindow.ShowCall();
			}
		}

		if (playAnimation && showAnimation != null)
		{
			await StartShowAnimation();
			isBusy = false;
		}

		if (blockInputs)
		{
			UIManager.Instance.BlockInputs(this);
		}

	}

	protected virtual void _Show()
	{
		
	}

	public async Task HideCall(bool playAnimation = true)
	{
		if (playAnimation && hideAnimation != null)
		{
			await StartHideAnimation();
			isBusy = false;
		}

		_Hide();
		Hide();
		foreach (var uiElement in uiElements)
		{
			if (uiElement is UIWindow uiWindow)
			{
				if(uiWindow.GetParent() == this) continue;
				await uiWindow.HideCall();
			}
		}
		Visual?.Hide();
		IsShown = false;
		MouseFilter = MouseFilterEnum.Ignore;
		if (blockInputs)
		{
			UIManager.Instance.UnblockInputs(this);
		}
	}

	protected virtual void _Hide()
	{
		
	}


	protected abstract Task DrawUI();
	#region Animation Functions

	protected async Task StartShowAnimation()
	{
		isBusy = true;
		Tween animationTween = showAnimation.createAnimationTween(this);
		await ToSignal(animationTween, Tween.SignalName.Finished);
	}
	protected  async Task  StartHideAnimation()
	{
		isBusy = true;
		Tween animationTween = hideAnimation.createAnimationTween(this);
		await ToSignal(animationTween, Tween.SignalName.Finished);
	}
	
	#endregion
	/// <summary>
	/// Finds all UIElements that belong to this window, excluding those that belong to child UIWindows
	/// </summary>
	/// <returns>List of UIElements belonging to this window, including child UIWindows but not their contents</returns>
	public List<UIElement> GetChildUIElements()
	{
		var result = new List<UIElement>();
        
		// Start searching from this window's children, not the window itself
		foreach (Node child in this.GetChildren())
		{
			SearchUIElementsRecursively(child, result);
		}
        
		return result;
	}

	private static void SearchUIElementsRecursively(Node node, List<UIElement> result)
	{
		// If we hit a UIWindow, add it but don't search its children
		if (node is UIWindow uiWindow)
		{
			result.Add(uiWindow);
			return; 
		}
        

		if (node is UIElement uiElement)
		{
			result.Add(uiElement);
		}
		
		foreach (Node child in node.GetChildren())
		{
			SearchUIElementsRecursively(child, result);
		}
	}
	
	public Control GetVisual() => Visual;


	public override void _EnterTree()
	{
		base._EnterTree();
		AddToGroup("UIWindows");
		if (toggleButton != null)
		{
			toggleButton.Pressed += ToggleButtonOnPressed;
		}
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (toggleButton != null)
		{
			toggleButton.Pressed -= ToggleButtonOnPressed;
		}
	}
}
