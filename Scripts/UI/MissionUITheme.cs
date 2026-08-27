using Godot;

public static class MissionUITheme
{
	public static readonly Color BackdropColor = new(0.015f, 0.02f, 0.03f, 0.94f);
	public static readonly Color PanelColor = new(0.07f, 0.085f, 0.11f, 1f);
	public static readonly Color PanelRaisedColor = new(0.095f, 0.115f, 0.145f, 1f);
	public static readonly Color FieldColor = new(0.035f, 0.045f, 0.065f, 1f);
	public static readonly Color BorderColor = new(0.3f, 0.38f, 0.48f, 1f);
	public static readonly Color AccentColor = new(0.32f, 0.58f, 0.82f, 1f);
	public static readonly Color TextColor = new(0.9f, 0.93f, 0.97f, 1f);
	public static readonly Color MutedTextColor = new(0.62f, 0.68f, 0.76f, 1f);

	private static Theme _theme;

	public static Theme Shared => _theme ??= CreateTheme();

	public static void Apply(Control root, bool replaceLocalStyles = false)
	{
		if (root == null) return;
		root.Theme = Shared;
		if (replaceLocalStyles) RemoveLocalStyleOverrides(root);
	}

	public static void StyleFirstTitle(Control root, int fontSize = 26)
	{
		Label title = FindFirstLabel(root);
		if (title == null) return;
		title.AddThemeFontSizeOverride("font_size", fontSize);
		title.AddThemeColorOverride("font_color", TextColor);
	}

	public static void StyleTitle(Label title, int fontSize = 26)
	{
		if (title == null) return;
		title.AddThemeFontSizeOverride("font_size", fontSize);
		title.AddThemeColorOverride("font_color", TextColor);
	}

	public static void StylePanel(Panel panel)
	{
		panel?.AddThemeStyleboxOverride("panel", CreatePanelStyle());
	}

	public static void StyleButton(Button button)
	{
		if (button == null) return;
		foreach (string style in new[] { "normal", "hover", "pressed", "focus", "disabled" })
		{
			button.AddThemeStyleboxOverride(
				style,
				Shared.GetStylebox(style, "Button"));
		}
		button.AddThemeColorOverride("font_color", TextColor);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		button.AddThemeColorOverride(
			"font_disabled_color",
			MutedTextColor.Darkened(0.25f));
	}

	public static void StyleProgressBar(ProgressBar progressBar, Color fillColor)
	{
		if (progressBar == null) return;
		progressBar.AddThemeStyleboxOverride(
			"background",
			CreateBox(FieldColor, BorderColor.Darkened(0.25f), 4, 1));
		progressBar.AddThemeStyleboxOverride(
			"fill",
			CreateBox(fillColor, fillColor.Lightened(0.12f), 4, 1));
		progressBar.AddThemeColorOverride("font_color", Colors.White);
	}

	public static void NormalizeButtonText(Control root)
	{
		if (root == null) return;
		foreach (Node child in root.GetChildren())
		{
			if (child is Button button) button.Text = button.Text.Trim();
			if (child is Control control) NormalizeButtonText(control);
		}
	}

	public static void InsetPanelContent(Control root, int inset = 14)
	{
		Panel panel = root?.GetNodeOrNull<Panel>("Panel");
		if (panel == null) return;

		foreach (Node child in panel.GetChildren())
		{
			if (child is not Control content ||
				!Mathf.IsEqualApprox(content.AnchorLeft, 0f) ||
				!Mathf.IsEqualApprox(content.AnchorTop, 0f) ||
				!Mathf.IsEqualApprox(content.AnchorRight, 1f) ||
				!Mathf.IsEqualApprox(content.AnchorBottom, 1f))
			{
				continue;
			}

			if (content is MarginContainer margin)
			{
				SetMinimumMargin(margin, "margin_left", inset);
				SetMinimumMargin(margin, "margin_top", inset);
				SetMinimumMargin(margin, "margin_right", inset);
				SetMinimumMargin(margin, "margin_bottom", inset);
			}
			else
			{
				content.OffsetLeft = Mathf.Max(content.OffsetLeft, inset);
				content.OffsetTop = Mathf.Max(content.OffsetTop, inset);
				content.OffsetRight = Mathf.Min(content.OffsetRight, -inset);
				content.OffsetBottom = Mathf.Min(content.OffsetBottom, -inset);
			}
		}
	}

	private static void SetMinimumMargin(
		MarginContainer container,
		string marginName,
		int minimum)
	{
		int current = container.HasThemeConstantOverride(marginName)
			? container.GetThemeConstant(marginName)
			: 0;
		container.AddThemeConstantOverride(marginName, Mathf.Max(current, minimum));
	}

	public static StyleBoxFlat CreatePanelStyle()
	{
		return CreateBox(
			PanelColor,
			BorderColor,
			8,
			1,
			new Color(0f, 0f, 0f, 0.45f),
			10);
	}

	private static Theme CreateTheme()
	{
		var theme = new Theme
		{
			DefaultFontSize = 16
		};

		theme.SetColor("font_color", "Label", TextColor);
		theme.SetColor("font_shadow_color", "Label", new Color(0f, 0f, 0f, 0.65f));
		theme.SetConstant("shadow_offset_x", "Label", 1);
		theme.SetConstant("shadow_offset_y", "Label", 1);

		StyleBoxFlat panel = CreatePanelStyle();
		theme.SetStylebox("panel", "Panel", panel);
		theme.SetStylebox("panel", "PanelContainer", panel);
		theme.SetStylebox("panel", "PopupPanel", panel);

		StyleBoxFlat buttonNormal = CreateBox(
			PanelRaisedColor,
			BorderColor,
			5,
			1);
		StyleBoxFlat buttonHover = CreateBox(
			new Color(0.13f, 0.18f, 0.24f, 1f),
			AccentColor,
			5,
			1);
		StyleBoxFlat buttonPressed = CreateBox(
			new Color(0.075f, 0.13f, 0.19f, 1f),
			AccentColor,
			5,
			2);
		StyleBoxFlat buttonDisabled = CreateBox(
			new Color(0.055f, 0.065f, 0.08f, 0.9f),
			new Color(0.18f, 0.21f, 0.25f, 1f),
			5,
			1);
		theme.SetStylebox("normal", "Button", buttonNormal);
		theme.SetStylebox("hover", "Button", buttonHover);
		theme.SetStylebox("pressed", "Button", buttonPressed);
		theme.SetStylebox("focus", "Button", buttonHover);
		theme.SetStylebox("disabled", "Button", buttonDisabled);
		theme.SetColor("font_color", "Button", TextColor);
		theme.SetColor("font_hover_color", "Button", Colors.White);
		theme.SetColor("font_pressed_color", "Button", Colors.White);
		theme.SetColor("font_focus_color", "Button", Colors.White);
		theme.SetColor("font_disabled_color", "Button", MutedTextColor.Darkened(0.25f));
		theme.SetConstant("outline_size", "Button", 0);

		StyleBoxFlat field = CreateBox(FieldColor, BorderColor.Darkened(0.2f), 4, 1);
		StyleBoxFlat fieldFocus = CreateBox(FieldColor, AccentColor, 4, 1);
		foreach (string type in new[] { "Tree", "ItemList", "LineEdit", "TextEdit" })
		{
			theme.SetStylebox("panel", type, field);
			theme.SetStylebox("normal", type, field);
			theme.SetStylebox("focus", type, fieldFocus);
			theme.SetColor("font_color", type, TextColor);
			theme.SetColor("font_selected_color", type, Colors.White);
			theme.SetColor("font_readonly_color", type, MutedTextColor);
		}

		StyleBoxFlat selection = CreateBox(
			new Color(0.12f, 0.25f, 0.38f, 1f),
			AccentColor,
			3,
			1);
		theme.SetStylebox("selected", "Tree", selection);
		theme.SetStylebox("selected_focus", "Tree", selection);
		theme.SetStylebox("selected", "ItemList", selection);
		theme.SetStylebox("selected_focus", "ItemList", selection);

		theme.SetStylebox("panel", "TabContainer", field);
		StyleBoxFlat tabSelected = CreateBox(PanelRaisedColor, AccentColor, 4, 1);
		StyleBoxFlat tabUnselected = CreateBox(FieldColor, BorderColor.Darkened(0.25f), 4, 1);
		theme.SetStylebox("tab_selected", "TabBar", tabSelected);
		theme.SetStylebox("tab_hovered", "TabBar", buttonHover);
		theme.SetStylebox("tab_unselected", "TabBar", tabUnselected);
		theme.SetStylebox("tab_disabled", "TabBar", buttonDisabled);
		theme.SetColor("font_selected_color", "TabBar", Colors.White);
		theme.SetColor("font_unselected_color", "TabBar", MutedTextColor);

		StyleBoxFlat separator = new()
		{
			BgColor = new Color(0.22f, 0.3f, 0.39f, 1f),
			ContentMarginTop = 1,
			ContentMarginBottom = 1
		};
		theme.SetStylebox("separator", "HSeparator", separator);
		theme.SetStylebox("separator", "VSeparator", separator);
		theme.SetStylebox(
			"background",
			"ProgressBar",
			CreateBox(FieldColor, BorderColor.Darkened(0.25f), 4, 1));
		theme.SetStylebox(
			"fill",
			"ProgressBar",
			CreateBox(AccentColor, AccentColor.Lightened(0.12f), 4, 1));
		theme.SetColor("font_color", "ProgressBar", Colors.White);
		theme.SetConstant("separation", "VBoxContainer", 10);
		theme.SetConstant("separation", "HBoxContainer", 8);

		return theme;
	}

	private static StyleBoxFlat CreateBox(
		Color background,
		Color border,
		int radius,
		int borderWidth,
		Color shadow = default,
		int shadowSize = 0)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7
		};
		if (shadowSize > 0)
		{
			style.ShadowColor = shadow;
			style.ShadowSize = shadowSize;
		}
		return style;
	}

	private static Label FindFirstLabel(Node node)
	{
		if (node == null) return null;
		foreach (Node child in node.GetChildren())
		{
			if (child is Label label) return label;
			Label nested = FindFirstLabel(child);
			if (nested != null) return nested;
		}
		return null;
	}

	private static void RemoveLocalStyleOverrides(Control root)
	{
		switch (root)
		{
			case Button button:
				foreach (string style in new[] { "normal", "hover", "pressed", "focus", "disabled" })
					button.RemoveThemeStyleboxOverride(style);
				break;
			case Panel panel:
				panel.RemoveThemeStyleboxOverride("panel");
				break;
			case PanelContainer panelContainer:
				panelContainer.RemoveThemeStyleboxOverride("panel");
				break;
			case HSeparator horizontalSeparator:
				horizontalSeparator.RemoveThemeStyleboxOverride("separator");
				break;
			case VSeparator verticalSeparator:
				verticalSeparator.RemoveThemeStyleboxOverride("separator");
				break;
		}

		foreach (Node child in root.GetChildren())
		{
			if (child is Control control) RemoveLocalStyleOverrides(control);
		}
	}
}
