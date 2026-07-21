#if TOOLS
using Godot;
using System;

[Tool]
public partial class CountryMapPicker : TextureRect
{
	public event Action<Vector2I> PixelPicked;

	public CountryMapPicker()
	{
		ExpandMode = ExpandModeEnum.IgnoreSize;
		StretchMode = StretchModeEnum.KeepAspectCentered;
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton mouse
		    || mouse.ButtonIndex != MouseButton.Left
		    || !mouse.Pressed
		    || Texture == null)
			return;

		Rect2 drawnRect = GetDrawnTextureRect();
		if (!drawnRect.HasPoint(mouse.Position))
			return;

		Vector2 normalized = (mouse.Position - drawnRect.Position) / drawnRect.Size;
		Vector2 textureSize = Texture.GetSize();
		var pixel = new Vector2I(
			Mathf.Clamp((int)(normalized.X * textureSize.X), 0, (int)textureSize.X - 1),
			Mathf.Clamp((int)(normalized.Y * textureSize.Y), 0, (int)textureSize.Y - 1)
		);

		PixelPicked?.Invoke(pixel);
		AcceptEvent();
	}

	private Rect2 GetDrawnTextureRect()
	{
		Vector2 textureSize = Texture.GetSize();
		if (textureSize.X <= 0 || textureSize.Y <= 0)
			return new Rect2();

		float scale = Mathf.Min(Size.X / textureSize.X, Size.Y / textureSize.Y);
		Vector2 drawnSize = textureSize * scale;
		return new Rect2((Size - drawnSize) * 0.5f, drawnSize);
	}
}
#endif
