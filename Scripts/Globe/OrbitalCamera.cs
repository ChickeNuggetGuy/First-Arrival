using Godot;
using System;
using System.Threading.Tasks;

[GlobalClass]
public partial class OrbitalCamera : Node3D
{
	public static OrbitalCamera Instance;
    [ExportGroup("Settings")]
    [Export] public float MouseSensitivity = 0.3f;
    [Export] public float KeySensitivity = 2.0f;
    [Export] public bool InvertY = false;
    [Export] public float ScrollSpeed = 2.0f;
    [Export] public bool UseSmoothing = true;
    [Export] public float SmoothSpeed = 10.0f;

    [ExportGroup("Panning")]
    [Export] public float PanSpeed = 90.0f;
    [Export(PropertyHint.Range, "0, 2, 0.01")] public float MinZoomPanSpeedMultiplier = 0.5f;
    [Export(PropertyHint.Range, "0, 2, 0.01")] public float MaxZoomPanSpeedMultiplier = 1.0f;
    [Export(PropertyHint.Range, "0.05, 2, 0.01")] public float FocusToleranceDegrees = 0.25f;
    [Export(PropertyHint.Range, "0.1, 10, 0.1")] public float FocusTimeoutSeconds = 3.0f;

    [ExportGroup("Auto Orbit")]
    [Export] public bool AutoOrbit = false;
    [Export] public float AutoOrbitSpeed = 8.0f;

    [ExportGroup("Limits")]
    [Export] public float MinPitch = -89.0f; // Prevent looking straight up/flipping
    [Export] public float MaxPitch = 89.0f;  // Prevent looking straight down
    [Export] public float MinZoom = 2.0f;
    [Export] public float MaxZoom = 20.0f;
    
    private float _pitch = 0.0f;
    private float _yaw = 0.0f;
    
    // Target Zoom (distance)
    private float _targetDistance = 5.0f;

    // Child camera reference
    private Camera3D _camera;

    public override void _Ready()
    {
        // Find the child camera
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        
        if (_camera == null)
        {
            GD.PrintErr("OrbitalCamera: No Camera3D child found! Please add one.");
            SetProcess(false);
            return;
        }

        // Initialize values based on current editor transform
        Vector3 initialRotation = RotationDegrees;
        _yaw = float.IsFinite(initialRotation.Y) ? initialRotation.Y : 0.0f;
        _pitch = float.IsFinite(initialRotation.X) ? initialRotation.X : 0.0f;
        ClampPitch();
        _targetDistance = Mathf.Clamp(_camera.Position.Z, MinZoom, MaxZoom);

        // Apply the limit immediately so smoothing cannot leave the camera
        // inside the orbited object during the first frames of the scene.
        Vector3 initialCameraPosition = _camera.Position;
        initialCameraPosition.Z = _targetDistance;
        _camera.Position = initialCameraPosition;
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;

        base._ExitTree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Mouse Rotation (Only when Right Click is held)
        if (@event is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _yaw -= mouseMotion.Relative.X * MouseSensitivity;
            
            float pitchDelta = mouseMotion.Relative.Y * MouseSensitivity;
            if (InvertY) _pitch -= pitchDelta;
            else _pitch -= pitchDelta;

            ClampPitch();
        }

        // Mouse Zoom (Scroll Wheel)
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _targetDistance -= ScrollSpeed;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _targetDistance += ScrollSpeed;
            }
            
            _targetDistance = Mathf.Clamp(_targetDistance, MinZoom, MaxZoom);
        }
    }

    public override void _Process(double delta)
    {
        RecoverInvalidOrbitState();
        HandleKeyboardInput((float)delta);
        UpdateTransform((float)delta);
    }

    private void HandleKeyboardInput(float delta)
    {
        // Use the project's physical-key actions instead of polling logical
        // key codes. This keeps WASD working regardless of keyboard layout or
        // which Control currently owns keyboard focus.
        float panHorizontal = Input.GetAxis("cameraLeft", "cameraRight");
        float panVertical = Input.GetAxis("cameraUp", "cameraDown");

        if (!Mathf.IsZeroApprox(panHorizontal) || !Mathf.IsZeroApprox(panVertical))
        {
            float zoomRange = MaxZoom - MinZoom;
            float zoomT = Mathf.IsZeroApprox(zoomRange)
                ? 0.0f
                : Mathf.Clamp((_targetDistance - MinZoom) / zoomRange, 0.0f, 1.0f);
            float zoomAdjustedPanSpeed = PanSpeed * Mathf.Lerp(
                MinZoomPanSpeedMultiplier,
                MaxZoomPanSpeedMultiplier,
                zoomT);

            Vector2 panInput = new Vector2(panHorizontal, panVertical).Normalized();
            _yaw += panInput.X * zoomAdjustedPanSpeed * delta;
            _pitch += panInput.Y * zoomAdjustedPanSpeed * delta;
            ClampPitch();
        }

        if (AutoOrbit)
        {
            _yaw += AutoOrbitSpeed * delta;
        }
    }

    private void ClampPitch()
    {
        if (!float.IsFinite(_pitch))
            _pitch = 0.0f;
        if (!float.IsFinite(_yaw))
            _yaw = 0.0f;

        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
    }

    private void UpdateTransform(float delta)
    {
        Vector3 targetRotation = new Vector3(Mathf.DegToRad(_pitch), Mathf.DegToRad(_yaw), 0);

        if (UseSmoothing && SmoothSpeed > 0.0f)
        {
            // Interpolate each orbit axis directly. Converting a smoothed
            // quaternion back to Euler angles can become unstable near the
            // pitch limits and leave rotation frozen while zoom still works.
            float weight = 1.0f - Mathf.Exp(-SmoothSpeed * Mathf.Max(delta, 0.0f));
            Vector3 currentRotation = Rotation;
            Rotation = new Vector3(
                Mathf.LerpAngle(currentRotation.X, targetRotation.X, weight),
                Mathf.LerpAngle(currentRotation.Y, targetRotation.Y, weight),
                0.0f);
        }
        else
        {
            Rotation = targetRotation;
        }


        if (_camera != null)
        {
            Vector3 camPos = _camera.Position;
            if (UseSmoothing && SmoothSpeed > 0.0f)
            {
                float weight =
                    1.0f - Mathf.Exp(-SmoothSpeed * Mathf.Max(delta, 0.0f));
                camPos.Z = Mathf.Lerp(camPos.Z, _targetDistance, weight);
            }
            else
            {
                camPos.Z = _targetDistance;
            }
            _camera.Position = camPos;
        }
    }
    
    
    /// <summary>
    /// Smoothly rotates the camera to focus on a specific hex cell.
    /// </summary>
    public void FocusOnCell(HexCellData cell, float? optionalZoom = null)
    {
	    SetFocusTarget(cell, optionalZoom);
    }
    
    /// <summary>
    /// Focuses a cell and completes when the smoothed camera motion settles.
    /// Callers that transition away from the globe can await this method so the
    /// player sees the pan before the globe scene is removed.
    /// </summary>
    public async Task FocusOnCell(int cellIndex, float? optionalZoom = null)
    {
	    HexCellData? cell = GlobeHexGridManager.Instance?.GetCellFromIndex(cellIndex);
	    
	    if (!cell.HasValue) return;

	    SetFocusTarget(cell.Value, optionalZoom);
	    await WaitForFocus();
    }

    private void SetFocusTarget(HexCellData cell, float? optionalZoom)
    {
	    if (cell.Center.LengthSquared() <= Mathf.Epsilon)
		    return;

	    Vector3 dir = cell.Center.Normalized();
	    _yaw = Mathf.RadToDeg(Mathf.Atan2(dir.X, dir.Z));
	    _pitch = -Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(dir.Y, -1.0f, 1.0f)));

	    if (optionalZoom.HasValue)
		    _targetDistance = Mathf.Clamp(optionalZoom.Value, MinZoom, MaxZoom);

	    ClampPitch();
    }

    private void RecoverInvalidOrbitState()
    {
        Vector3 currentRotation = Rotation;
        bool invalidRotation =
            !float.IsFinite(currentRotation.X) ||
            !float.IsFinite(currentRotation.Y) ||
            !float.IsFinite(currentRotation.Z);
        bool invalidTarget = !float.IsFinite(_pitch) || !float.IsFinite(_yaw);

        if (!invalidRotation && !invalidTarget)
            return;

        GD.PushWarning("OrbitalCamera recovered from an invalid rotation state.");
        _pitch = 0.0f;
        _yaw = 0.0f;
        Rotation = Vector3.Zero;
    }

    private async Task WaitForFocus()
    {
	    if (!IsInsideTree()) return;

	    if (!UseSmoothing || SmoothSpeed <= 0.0f)
	    {
		    Rotation = new Vector3(Mathf.DegToRad(_pitch), Mathf.DegToRad(_yaw), 0.0f);
		    if (_camera != null)
		    {
			    Vector3 position = _camera.Position;
			    position.Z = _targetDistance;
			    _camera.Position = position;
		    }
		    return;
	    }

	    ulong timeoutAt = Time.GetTicksMsec() +
		    (ulong)(Mathf.Max(FocusTimeoutSeconds, 0.1f) * 1000.0f);
	    while (IsInsideTree() && Time.GetTicksMsec() < timeoutAt)
	    {
		    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		    if (HasReachedFocusTarget()) return;
	    }
    }

    private bool HasReachedFocusTarget()
    {
	    if (_camera == null) return true;

	    float pitchDelta = Mathf.Abs(Mathf.AngleDifference(RotationDegrees.X, _pitch));
	    float yawDelta = Mathf.Abs(Mathf.AngleDifference(RotationDegrees.Y, _yaw));
	    float zoomDelta = Mathf.Abs(_camera.Position.Z - _targetDistance);

	    return pitchDelta <= FocusToleranceDegrees &&
		    yawDelta <= FocusToleranceDegrees &&
		    zoomDelta <= 0.01f;
    }
}
