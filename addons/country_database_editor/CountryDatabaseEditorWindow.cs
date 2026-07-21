#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Globe.Countries;

[Tool]
public partial class CountryDatabaseEditorWindow : Control
{
	private const string DatabasePath = "res://Data/Countries/CountryDatabase.tres";

	private CountryDatabase _database;
	private CountryDefinition _selectedCountry;
	private Image _mapImage;
	private byte[] _mapBytes = Array.Empty<byte>();
	private readonly Dictionary<uint, Rect2I> _countryBounds = new();

	private CountryMapPicker _worldMap;
	private TextureRect _shapePreview;
	private Label _selectionTitle;
	private Label _keyLabel;
	private Label _statusLabel;
	private ColorRect _colorSwatch;
	private LineEdit _nameEdit;
	private LineEdit _codeEdit;
	private SpinBox _gdpEdit;
	private SpinBox _opinionEdit;
	private Button _saveButton;
	private Button _inspectorButton;
	private ShaderMaterial _worldHighlightMaterial;
	private ShaderMaterial _previewHighlightMaterial;
	private bool _updatingFields;
	private bool _databaseLoaded;

	private static readonly string HighlightShaderCode = """
		shader_type canvas_item;

		uniform vec4 selected_color : source_color = vec4(1.0);
		uniform bool has_selection = false;

		void fragment() {
			vec4 source = texture(TEXTURE, UV);
			if (!has_selection) {
				COLOR = source;
			} else {
				float color_distance = distance(source.rgb, selected_color.rgb);
				if (source.a > 0.1 && color_distance < 0.0025) {
					COLOR = vec4(mix(source.rgb, vec3(1.0), 0.3), 1.0);
				} else {
					float luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
					COLOR = vec4(vec3(luminance) * 0.22, source.a * 0.62);
				}
			}
		}
		""";

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildInterface();
		_statusLabel.Text = "Open this tab to load the country map.";
	}

	public void EnsureLoaded()
	{
		if (!_databaseLoaded)
			LoadDatabase();
	}

	private void BuildInterface()
	{
		var root = new VBoxContainer();
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("separation", 8);
		AddChild(root);

		var toolbar = new HBoxContainer();
		root.AddChild(toolbar);

		var heading = new Label
		{
			Text = "Country Database",
			ThemeTypeVariation = "HeaderLarge"
		};
		toolbar.AddChild(heading);

		toolbar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

		var reloadButton = new Button { Text = "Reload Database" };
		reloadButton.Pressed += LoadDatabase;
		toolbar.AddChild(reloadButton);

		_saveButton = new Button { Text = "Save Changes", Disabled = true };
		_saveButton.Pressed += SaveDatabase;
		toolbar.AddChild(_saveButton);

		var split = new HSplitContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SplitOffsets = new[] { 850 }
		};
		root.AddChild(split);

		var mapPanel = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		split.AddChild(mapPanel);

		mapPanel.AddChild(new Label
		{
			Text = "Click inside a country to select it. The selected color region will remain bright.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		var mapFrame = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		mapPanel.AddChild(mapFrame);

		_worldMap = new CountryMapPicker
		{
			CustomMinimumSize = new Vector2(600, 350),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			TooltipText = "Click a colored country region"
		};
		_worldMap.PixelPicked += OnMapPixelPicked;
		_worldHighlightMaterial = CreateHighlightMaterial();
		_worldMap.Material = _worldHighlightMaterial;
		mapFrame.AddChild(_worldMap);

		var editorScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(360, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		split.AddChild(editorScroll);

		var editor = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(340, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		editorScroll.AddChild(editor);

		_selectionTitle = new Label
		{
			Text = "No country selected",
			ThemeTypeVariation = "HeaderLarge",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		editor.AddChild(_selectionTitle);

		var identityRow = new HBoxContainer();
		_colorSwatch = new ColorRect
		{
			Color = Colors.Transparent,
			CustomMinimumSize = new Vector2(42, 42),
			MouseFilter = MouseFilterEnum.Ignore
		};
		identityRow.AddChild(_colorSwatch);
		_keyLabel = new Label { Text = "Atlas color: —" };
		identityRow.AddChild(_keyLabel);
		editor.AddChild(identityRow);

		editor.AddChild(new Label { Text = "Selected Country Shape" });
		var previewFrame = new PanelContainer { CustomMinimumSize = new Vector2(320, 230) };
		editor.AddChild(previewFrame);
		_shapePreview = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_previewHighlightMaterial = CreateHighlightMaterial();
		_shapePreview.Material = _previewHighlightMaterial;
		previewFrame.AddChild(_shapePreview);

		editor.AddChild(new HSeparator());
		_nameEdit = AddLineEdit(editor, "Country Name", OnNameChanged);
		_codeEdit = AddLineEdit(editor, "Country Code", OnCodeChanged);
		_gdpEdit = AddSpinBox(editor, "Starting GDP", 0, 100000000000000.0, 1.0, OnGdpChanged);
		_opinionEdit = AddSpinBox(editor, "Starting Player Opinion", -100, 100, 0.1, OnOpinionChanged);

		_inspectorButton = new Button { Text = "Open Full Resource in Inspector", Disabled = true };
		_inspectorButton.Pressed += () =>
		{
			if (_selectedCountry != null)
				EditorInterface.Singleton.EditResource(_selectedCountry);
		};
		editor.AddChild(_inspectorButton);

		_statusLabel = new Label
		{
			Text = "Loading country database…",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_statusLabel);
	}

	private static ShaderMaterial CreateHighlightMaterial()
	{
		var shader = new Shader { Code = HighlightShaderCode };
		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("has_selection", false);
		return material;
	}

	private static LineEdit AddLineEdit(VBoxContainer parent, string label, Action<string> callback)
	{
		parent.AddChild(new Label { Text = label });
		var edit = new LineEdit { Editable = false };
		edit.TextChanged += value => callback(value);
		parent.AddChild(edit);
		return edit;
	}

	private static SpinBox AddSpinBox(
		VBoxContainer parent,
		string label,
		double minimum,
		double maximum,
		double step,
		Action<double> callback)
	{
		parent.AddChild(new Label { Text = label });
		var edit = new SpinBox
		{
			MinValue = minimum,
			MaxValue = maximum,
			Step = step,
			AllowGreater = true,
			Editable = false,
			// Committing on Enter/focus loss prevents large-step fields from
			// snapping partial input (for example "25") back to zero.
			UpdateOnTextChanged = false
		};
		edit.ValueChanged += value => callback(value);
		parent.AddChild(edit);
		return edit;
	}

	private void LoadDatabase()
	{
		_databaseLoaded = false;
		_selectedCountry = null;
		_database = ResourceLoader.Load<CountryDatabase>(
			DatabasePath,
			"",
			ResourceLoader.CacheMode.Replace
		);

		if (_database == null || _database.CountryMap == null)
		{
			_statusLabel.Text = $"Could not load a country database and map from {DatabasePath}.";
			SetEditorEnabled(false);
			return;
		}

		_database.RebuildLookup();
		_mapImage = _database.CountryMap.GetImage();
		if (_mapImage == null)
		{
			_statusLabel.Text = "The country map texture is not CPU-readable.";
			SetEditorEnabled(false);
			return;
		}

		if (_mapImage.IsCompressed() && _mapImage.Decompress() != Error.Ok)
		{
			_statusLabel.Text = "The country map texture could not be decompressed.";
			SetEditorEnabled(false);
			return;
		}

		if (_mapImage.GetFormat() != Image.Format.Rgba8)
			_mapImage.Convert(Image.Format.Rgba8);

		_mapBytes = _mapImage.GetData().ToArray();
		_worldMap.Texture = _database.CountryMap;
		_worldHighlightMaterial.SetShaderParameter("has_selection", false);
		BuildCountryBounds();
		ClearSelectionFields();
		SetEditorEnabled(false);
		_saveButton.Disabled = false;
		_databaseLoaded = true;
		_statusLabel.Text =
			$"Loaded {_database.Countries.Count} countries. Click the map to begin editing.";
	}

	private void BuildCountryBounds()
	{
		_countryBounds.Clear();
		var knownKeys = new HashSet<uint>(
			_database.Countries
				.Where(country => country != null)
				.Select(country => country.CountryKey)
		);

		int width = _mapImage.GetWidth();
		int height = _mapImage.GetHeight();
		var minimums = new Dictionary<uint, Vector2I>();
		var maximums = new Dictionary<uint, Vector2I>();

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				uint key = GetPixelKey(x, y);
				if (!knownKeys.Contains(key))
					continue;

				if (!minimums.TryGetValue(key, out Vector2I minimum))
				{
					minimums[key] = new Vector2I(x, y);
					maximums[key] = new Vector2I(x, y);
					continue;
				}

				Vector2I maximum = maximums[key];
				minimums[key] = new Vector2I(Math.Min(minimum.X, x), Math.Min(minimum.Y, y));
				maximums[key] = new Vector2I(Math.Max(maximum.X, x), Math.Max(maximum.Y, y));
			}
		}

		foreach ((uint key, Vector2I minimum) in minimums)
		{
			Vector2I maximum = maximums[key];
			_countryBounds[key] = new Rect2I(minimum, maximum - minimum + Vector2I.One);
		}
	}

	private void OnMapPixelPicked(Vector2I pixel)
	{
		CountryDefinition country = FindCountryNearPixel(pixel, 8);
		if (country == null)
		{
			_statusLabel.Text =
				"That pixel is not a database country. Try clicking farther inside a colored region.";
			return;
		}

		SelectCountry(country);
	}

	private CountryDefinition FindCountryNearPixel(Vector2I center, int searchRadius)
	{
		CountryDefinition exact = _database.GetCountry(GetPixelKey(center.X, center.Y));
		if (exact != null)
			return exact;

		for (int radius = 1; radius <= searchRadius; radius++)
		{
			for (int y = -radius; y <= radius; y++)
			{
				for (int x = -radius; x <= radius; x++)
				{
					if (Math.Abs(x) != radius && Math.Abs(y) != radius)
						continue;

					int px = center.X + x;
					int py = center.Y + y;
					if (px < 0 || py < 0 || px >= _mapImage.GetWidth() || py >= _mapImage.GetHeight())
						continue;

					CountryDefinition nearby = _database.GetCountry(GetPixelKey(px, py));
					if (nearby != null)
						return nearby;
				}
			}
		}

		return null;
	}

	private uint GetPixelKey(int x, int y)
	{
		int index = ((y * _mapImage.GetWidth()) + x) * 4;
		if (index < 0 || index + 3 >= _mapBytes.Length || _mapBytes[index + 3] <= 26)
			return 0;

		byte r = _mapBytes[index];
		byte g = _mapBytes[index + 1];
		byte b = _mapBytes[index + 2];
		if (r == 0 && g == 0 && b == 0)
			return 0;

		return (uint)(r | (g << 8) | (b << 16));
	}

	private void SelectCountry(CountryDefinition country)
	{
		_selectedCountry = country;
		_updatingFields = true;

		_selectionTitle.Text = country.CountryName;
		_colorSwatch.Color = country.MapColor;
		_keyLabel.Text = $"Atlas color: #{country.MapColor.ToHtml(false).ToUpperInvariant()}";
		_nameEdit.Text = country.CountryName;
		_codeEdit.Text = country.CountryCode;
		_gdpEdit.Value = country.GrossDomesticProduct;
		_opinionEdit.Value = country.PlayerOpinion;

		_updatingFields = false;
		SetEditorEnabled(true);
		_saveButton.Disabled = false;

		_worldHighlightMaterial.SetShaderParameter("selected_color", country.MapColor);
		_worldHighlightMaterial.SetShaderParameter("has_selection", true);
		_previewHighlightMaterial.SetShaderParameter("selected_color", country.MapColor);
		_previewHighlightMaterial.SetShaderParameter("has_selection", true);
		UpdateShapePreview(country.CountryKey);
		_statusLabel.Text = $"Editing {country.CountryName}.";
	}

	private void UpdateShapePreview(uint countryKey)
	{
		if (!_countryBounds.TryGetValue(countryKey, out Rect2I bounds))
		{
			_shapePreview.Texture = null;
			return;
		}

		int padding = Math.Max(10, Math.Max(bounds.Size.X, bounds.Size.Y) / 12);
		var padded = new Rect2I(
			bounds.Position - new Vector2I(padding, padding),
			bounds.Size + new Vector2I(padding * 2, padding * 2)
		).Intersection(new Rect2I(Vector2I.Zero, _mapImage.GetSize()));

		_shapePreview.Texture = new AtlasTexture
		{
			Atlas = _database.CountryMap,
			Region = padded,
			FilterClip = true
		};
	}

	private void SetEditorEnabled(bool enabled)
	{
		_nameEdit.Editable = enabled;
		_codeEdit.Editable = enabled;
		_gdpEdit.Editable = enabled;
		_opinionEdit.Editable = enabled;
		_inspectorButton.Disabled = !enabled;
	}

	private void ClearSelectionFields()
	{
		_updatingFields = true;
		_selectionTitle.Text = "No country selected";
		_keyLabel.Text = "Atlas color: —";
		_colorSwatch.Color = Colors.Transparent;
		_nameEdit.Text = "";
		_codeEdit.Text = "";
		_gdpEdit.Value = 0;
		_opinionEdit.Value = 0;
		_shapePreview.Texture = null;
		_updatingFields = false;
	}

	private void OnNameChanged(string value)
	{
		if (_updatingFields || _selectedCountry == null)
			return;

		_selectedCountry.CountryName = value;
		_selectionTitle.Text = value;
		MarkChanged();
	}

	private void OnCodeChanged(string value)
	{
		if (_updatingFields || _selectedCountry == null)
			return;

		_selectedCountry.CountryCode = value;
		MarkChanged();
	}

	private void OnGdpChanged(double value)
	{
		if (_updatingFields || _selectedCountry == null)
			return;

		_selectedCountry.GrossDomesticProduct = value;
		MarkChanged();
	}

	private void OnOpinionChanged(double value)
	{
		if (_updatingFields || _selectedCountry == null)
			return;

		_selectedCountry.PlayerOpinion = (float)value;
		MarkChanged();
	}

	private void MarkChanged()
	{
		_selectedCountry.EmitChanged();
		_database.EmitChanged();
		_statusLabel.Text = "Unsaved changes.";
	}

	private void SaveDatabase()
	{
		if (_database == null)
			return;

		Error error = ResourceSaver.Save(_database, DatabasePath);
		_statusLabel.Text = error == Error.Ok
			? $"Saved {DatabasePath}."
			: $"Could not save the country database: {error}.";

		if (error == Error.Ok)
			EditorInterface.Singleton.GetResourceFilesystem().Scan();
	}
}
#endif
