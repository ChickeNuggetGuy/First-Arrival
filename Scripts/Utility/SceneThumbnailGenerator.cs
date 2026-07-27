using System;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Renders a PackedScene or a Node3D into a one-shot ImageTexture.
///
/// Add this node to a scene to preview/save thumbnails in the editor, or call
/// GenerateThumbnailAsync at runtime. The returned texture owns a copy of the
/// rendered image and does not depend on this generator remaining alive.
/// </summary>
[Tool]
[GlobalClass]
public partial class SceneThumbnailGenerator : Node
{
	[Signal] public delegate void ThumbnailUpdatedEventHandler(Texture2D texture);
	[Signal] public delegate void ThumbnailSavedEventHandler(string path);

	[ExportGroup("Subject")]
	[Export] public PackedScene TargetScene { get; set; }
	[Export] public Node3D TargetNode { get; set; }
	[Export] public Vector3 ModelRotationDegrees { get; set; } = Vector3.Zero;
	[Export] public Vector3 ModelOffset { get; set; } = Vector3.Zero;

	[ExportGroup("Image")]
	[Export] public Vector2I ThumbnailSize { get; set; } = new(256, 256);
	[Export] public bool TransparentBackground { get; set; } = true;
	[Export(PropertyHint.ColorNoAlpha)]
	public Color BackgroundColor { get; set; } = new(0.17f, 0.17f, 0.21f);
	[Export(PropertyHint.SaveFile, "*.png")]
	public string OutputPath { get; set; } = "res://Thumbnails/thumbnail.png";

	[ExportGroup("Framing")]
	[Export] public bool Orthographic { get; set; } = true;
	[Export(PropertyHint.Range, "1,179,0.5,degrees")]
	public float FieldOfView { get; set; } = 35.0f;
	[Export(PropertyHint.Range, "1,4,0.01")]
	public float FramingMargin { get; set; } = 1.15f;
	[Export(PropertyHint.Range, "0.1,1,0.01")]
	public float VisibleHeightRatio { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float VerticalFocus { get; set; } = 0.5f;
	[Export] public Vector3 ViewDirection { get; set; } = new(0.8f, 0.15f, 1.0f);

	[ExportGroup("Lighting")]
	[Export(PropertyHint.ColorNoAlpha)]
	public Color AmbientColor { get; set; } = Colors.White;
	[Export(PropertyHint.Range, "0,8,0.05,or_greater")]
	public float AmbientEnergy { get; set; } = 0.8f;
	[Export(PropertyHint.ColorNoAlpha)]
	public Color KeyLightColor { get; set; } = Colors.White;
	[Export(PropertyHint.Range, "0,16,0.05,or_greater")]
	public float KeyLightEnergy { get; set; } = 1.4f;
	[Export] public Vector3 KeyLightRotationDegrees { get; set; } =
		new(-35.0f, -25.0f, 0.0f);
	[Export] public bool ShadowsEnabled { get; set; } = true;

	[ExportGroup("Actions")]
	[ExportToolButton("Refresh Thumbnail", Icon = "Reload")]
	public Callable RefreshThumbnailAction => Callable.From(RefreshThumbnail);
	[ExportToolButton("Save Thumbnail PNG", Icon = "Save")]
	public Callable SaveThumbnailAction => Callable.From(SaveThumbnail);

	public Texture2D LastThumbnail { get; private set; }

	private SubViewport _viewport;
	private Node3D _contentRoot;
	private Camera3D _camera;
	private Godot.Environment _environment;
	private DirectionalLight3D _keyLight;
	private Node _subject;
	private bool _captureInProgress;

	public override void _EnterTree()
	{
		EnsureSetup();
	}

	/// <summary>
	/// Generates a thumbnail from the configured TargetNode or TargetScene.
	/// TargetNode takes precedence when both are assigned.
	/// </summary>
	public async Task<Texture2D> GenerateThumbnailAsync()
	{
		if (TargetNode != null && GodotObject.IsInstanceValid(TargetNode))
			return await GenerateThumbnailAsync(TargetNode);

		if (TargetScene != null)
			return await GenerateThumbnailAsync(TargetScene);

		GD.PushWarning("SceneThumbnailGenerator: Assign a TargetNode or TargetScene.");
		return null;
	}

	/// <summary>
	/// Captures a copy of a live 3D node. The original node is never moved or changed.
	/// </summary>
	public async Task<Texture2D> GenerateThumbnailAsync(Node3D source)
	{
		if (source == null || !GodotObject.IsInstanceValid(source))
			return null;

		Node duplicate = source.Duplicate((int)Node.DuplicateFlags.UseInstantiation);
		if (duplicate == null)
		{
			GD.PushError(
				$"SceneThumbnailGenerator: Could not duplicate node {source.Name}.");
			return null;
		}

		return await CaptureAsync(duplicate);
	}

	/// <summary>
	/// Captures a packed scene without adding the gameplay instance to the main world.
	/// </summary>
	public async Task<Texture2D> GenerateThumbnailAsync(PackedScene source)
	{
		if (source == null)
			return null;

		Node instance = source.Instantiate();
		return await CaptureAsync(instance);
	}

	public Texture2D GetThumbnailTexture()
	{
		return LastThumbnail;
	}

	private async Task<Texture2D> CaptureAsync(Node subject)
	{
		if (subject == null)
			return null;

		if (!IsInsideTree())
		{
			subject.Free();
			GD.PushError(
				"SceneThumbnailGenerator must be inside the scene tree before capturing.");
			return null;
		}

		if (_captureInProgress)
		{
			GD.PushWarning(
				"SceneThumbnailGenerator: This generator is already capturing a thumbnail.");
			subject?.Free();
			return LastThumbnail;
		}

		_captureInProgress = true;
		try
		{
			EnsureSetup();
			RemoveSubject();
			_subject = subject;

			DisableGameplayProcessing(_subject);
			_contentRoot.Position = ModelOffset;
			_contentRoot.RotationDegrees = ModelRotationDegrees;
			_contentRoot.AddChild(_subject);

			ConfigureViewport();
			ConfigureCamera();
			_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

			// One frame schedules the one-shot viewport update; the second ensures
			// the render has completed before its image is copied off the GPU.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			Image image = _viewport.GetTexture().GetImage();
			if (image == null || image.IsEmpty())
			{
				GD.PushError("SceneThumbnailGenerator: The viewport produced no image.");
				return null;
			}

			LastThumbnail = ImageTexture.CreateFromImage(image);
			EmitSignal(SignalName.ThumbnailUpdated, LastThumbnail);
			return LastThumbnail;
		}
		finally
		{
			_captureInProgress = false;
		}
	}

	private void ConfigureViewport()
	{
		ThumbnailSize = new Vector2I(
			Math.Max(ThumbnailSize.X, 16),
			Math.Max(ThumbnailSize.Y, 16));

		_viewport.Size = ThumbnailSize;
		_viewport.TransparentBg = TransparentBackground;
		_viewport.RenderTargetClearMode = SubViewport.ClearMode.Always;

		_environment.BackgroundMode = TransparentBackground
			? Godot.Environment.BGMode.ClearColor
			: Godot.Environment.BGMode.Color;
		_environment.BackgroundColor = new Color(
			BackgroundColor.R,
			BackgroundColor.G,
			BackgroundColor.B,
			TransparentBackground ? 0.0f : 1.0f);
		_environment.AmbientLightColor = AmbientColor;
		_environment.AmbientLightEnergy = AmbientEnergy;

		_keyLight.LightColor = KeyLightColor;
		_keyLight.LightEnergy = KeyLightEnergy;
		_keyLight.RotationDegrees = KeyLightRotationDegrees;
		_keyLight.ShadowEnabled = ShadowsEnabled;
	}

	private void EnsureSetup()
	{
		if (GodotObject.IsInstanceValid(_viewport))
			return;

		_viewport = new SubViewport
		{
			Name = "ThumbnailViewport",
			OwnWorld3D = true,
			TransparentBg = TransparentBackground,
			RenderTargetClearMode = SubViewport.ClearMode.Always,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			Msaa3D = Viewport.Msaa.Msaa4X,
		};
		AddChild(_viewport, false, InternalMode.Back);

		_contentRoot = new Node3D { Name = "Subject" };
		_viewport.AddChild(_contentRoot);

		_camera = new Camera3D
		{
			Name = "ThumbnailCamera",
			Current = true,
			Near = 0.01f,
			Far = 10000.0f,
		};
		_viewport.AddChild(_camera);

		_environment = new Godot.Environment
		{
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
		};
		_viewport.AddChild(new WorldEnvironment
		{
			Name = "ThumbnailEnvironment",
			Environment = _environment,
		});

		_keyLight = new DirectionalLight3D { Name = "ThumbnailKeyLight" };
		_viewport.AddChild(_keyLight);
	}

	private void ConfigureCamera()
	{
		_camera.Fov = Mathf.Clamp(FieldOfView, 1.0f, 179.0f);
		_camera.Projection = Orthographic
			? Camera3D.ProjectionType.Orthogonal
			: Camera3D.ProjectionType.Perspective;

		Aabb bounds = CalculateVisualBounds();
		Aabb framedBounds = ApplyVerticalFraming(bounds);
		Vector3 center = framedBounds.GetCenter();
		float radius = Math.Max(framedBounds.Size.Length() * 0.5f, 0.05f);

		Vector3 direction = ViewDirection.Normalized();
		if (direction.IsZeroApprox())
			direction = new Vector3(0.8f, 0.15f, 1.0f).Normalized();

		float aspect = ThumbnailSize.X / Math.Max((float)ThumbnailSize.Y, 1.0f);
		float aspectCorrection = 1.0f / Math.Min(aspect, 1.0f);

		if (Orthographic)
		{
			_camera.Size = radius * 2.0f * FramingMargin * aspectCorrection;
			LookAtSafely(center + direction * Math.Max(radius * 3.0f, 1.0f), center);
			return;
		}

		float halfFov = Mathf.DegToRad(_camera.Fov * 0.5f);
		float distance = radius * FramingMargin * aspectCorrection
			/ Math.Max(Mathf.Tan(halfFov), 0.001f);
		LookAtSafely(center + direction * distance, center);
	}

	private Aabb CalculateVisualBounds()
	{
		Aabb combined = default;
		bool foundVisual = false;

		foreach (Node node in _contentRoot.FindChildren(
			"*",
			nameof(VisualInstance3D),
			true,
			false))
		{
			if (node is not VisualInstance3D visual || !visual.Visible)
				continue;

			Aabb visualBounds = visual.GlobalTransform * visual.GetAabb();
			if (visualBounds.Size.LengthSquared() <= 0.0f)
				continue;

			combined = foundVisual ? combined.Merge(visualBounds) : visualBounds;
			foundVisual = true;
		}

		return foundVisual
			? combined
			: new Aabb(ModelOffset - Vector3.One * 0.5f, Vector3.One);
	}

	private Aabb ApplyVerticalFraming(Aabb bounds)
	{
		float ratio = Mathf.Clamp(VisibleHeightRatio, 0.1f, 1.0f);
		float height = Math.Max(bounds.Size.Y * ratio, 0.01f);
		float desiredCenter = bounds.Position.Y
			+ bounds.Size.Y * Mathf.Clamp(VerticalFocus, 0.0f, 1.0f);
		float minCenter = bounds.Position.Y + height * 0.5f;
		float maxCenter = bounds.End.Y - height * 0.5f;
		float centerY = minCenter <= maxCenter
			? Mathf.Clamp(desiredCenter, minCenter, maxCenter)
			: bounds.GetCenter().Y;

		return new Aabb(
			new Vector3(bounds.Position.X, centerY - height * 0.5f, bounds.Position.Z),
			new Vector3(bounds.Size.X, height, bounds.Size.Z));
	}

	private void LookAtSafely(Vector3 cameraPosition, Vector3 target)
	{
		if (cameraPosition.IsEqualApprox(target))
			cameraPosition += Vector3.Back;

		_camera.Position = cameraPosition;
		Vector3 lookDirection = (target - cameraPosition).Normalized();
		Vector3 up = Math.Abs(lookDirection.Dot(Vector3.Up)) > 0.999f
			? Vector3.Forward
			: Vector3.Up;
		_camera.LookAt(target, up);
	}

	private static void DisableGameplayProcessing(Node node)
	{
		if (node == null)
			return;

		node.ProcessMode = ProcessModeEnum.Disabled;

		if (node is CollisionObject3D collision)
		{
			collision.CollisionLayer = 0;
			collision.CollisionMask = 0;
		}

		if (node is AnimationTree animationTree)
			animationTree.Active = false;

		if (node is AnimationPlayer animationPlayer)
			animationPlayer.Stop();

		foreach (Node child in node.GetChildren(true))
			DisableGameplayProcessing(child);
	}

	private void RemoveSubject()
	{
		if (!GodotObject.IsInstanceValid(_subject))
			return;

		_subject.Free();
		_subject = null;
	}

	private async void RefreshThumbnail()
	{
		await GenerateThumbnailAsync();
	}

	private async void SaveThumbnail()
	{
		Texture2D texture = await GenerateThumbnailAsync();
		if (texture == null || string.IsNullOrWhiteSpace(OutputPath))
			return;

		string savePath = ResolveOutputPath(OutputPath);
		if (!savePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
			savePath += ".png";

		if (!Engine.IsEditorHint() &&
		    savePath.StartsWith("res://", StringComparison.Ordinal))
		{
			GD.PushError(
				"SceneThumbnailGenerator: Runtime PNG files must use a user:// path.");
			return;
		}

		string absolutePath = ProjectSettings.GlobalizePath(savePath);
		Error directoryError =
			DirAccess.MakeDirRecursiveAbsolute(absolutePath.GetBaseDir());
		if (directoryError != Error.Ok)
		{
			GD.PushError(
				$"SceneThumbnailGenerator: Could not create output directory: {directoryError}");
			return;
		}

		Error saveError = texture.GetImage().SavePng(absolutePath);
		if (saveError != Error.Ok)
		{
			GD.PushError(
				$"SceneThumbnailGenerator: Could not save PNG: {saveError}");
			return;
		}

		if (Engine.IsEditorHint() &&
		    savePath.StartsWith("res://", StringComparison.Ordinal))
		{
			EditorInterface.Singleton.GetResourceFilesystem().Scan();
		}

		EmitSignal(SignalName.ThumbnailSaved, savePath);
	}

	private static string ResolveOutputPath(string path)
	{
		if (!path.StartsWith("uid://", StringComparison.Ordinal))
			return path;

		long uid = ResourceUid.TextToId(path);
		string resolvedPath = ResourceUid.GetIdPath(uid);
		return string.IsNullOrWhiteSpace(resolvedPath) ? path : resolvedPath;
	}
}
