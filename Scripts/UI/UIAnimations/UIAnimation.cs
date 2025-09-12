using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.UI.UIAnimations;

[GlobalClass]
public abstract partial class UIAnimation : Resource
{
	[Export] protected float duration; 
	
	
	
	public abstract Tween createAnimationTween(UIWindow window);
}