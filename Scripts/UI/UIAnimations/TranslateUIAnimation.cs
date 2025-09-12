using Godot;

namespace FirstArrival.Scripts.UI.UIAnimations;

[GlobalClass]
public partial class TranslateUIAnimation : UIAnimation
{
	[Export] protected Vector2 position;

	public TranslateUIAnimation()
	{
		duration = 0.2f;
		position = new Vector2(0, 0);
	}

	public override Tween createAnimationTween(UIWindow window)
	{
		Tween windowTween = window.CreateTween();
		windowTween.TweenProperty(window.GetVisual(), "position", position, duration);
		return windowTween;
	}
}