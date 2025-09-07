using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

[GlobalClass]
public partial class UIWindow : UIElement
{
	[Export] protected Control Visual;
	[Export] public string uiName { get; private set; }
	[Export] protected Key toggleKey { get; private set; }
	[Export] bool startHidden = false;
	protected List<UIElement> uiElements = new List<UIElement>();
	
	public bool IsShown {get; private set;}

	protected override async Task _Setup()
	{
		// Find all UIElements that belong to this window
		uiElements = GetChildUIElements();
    
		// Now you can work with the UIElements that belong to this window
		foreach (var element in uiElements)
		{
			await element.SetupCall();
		}

		if (startHidden)
		{
			HideCall();
		}
		
		return;
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (@event is InputEventKey inputEventKey && inputEventKey.Keycode == toggleKey && @event.IsPressed())
		{
			Toggle();
		}
	}

	public void Toggle()
	{
		if (IsShown)
		{
			HideCall();
		}
		else
		{
			ShowCall();
		}
	}

	public void ShowCall()
	{
		_Show();
		Visual.Show();
		IsShown = true;
	}

	protected virtual void _Show()
	{
		
	}

	public void HideCall()
	{
		_Hide();
		Visual.Hide();
		IsShown = false;
	}

	protected virtual void _Hide()
	{
		
	}
	
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
}
