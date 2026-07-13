using Godot;
using System;

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
        _camera = GetNode<Camera3D>("Camera3D");
        
        if (_camera == null)
        {
            GD.PrintErr("OrbitalCamera: No Camera3D child found! Please add one.");
            SetProcess(false);
            return;
        }

        // Initialize values based on current editor transform
        _yaw = RotationDegrees.Y;
        _pitch = RotationDegrees.X;
        _targetDistance = Mathf.Clamp(_camera.Position.Z, MinZoom, MaxZoom);

        // Apply the limit immediately so smoothing cannot leave the camera
        // inside the orbited object during the first frames of the scene.
        Vector3 initialCameraPosition = _camera.Position;
        initialCameraPosition.Z = _targetDistance;
        _camera.Position = initialCameraPosition;
        Instance = this;
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
        HandleKeyboardInput((float)delta);
        UpdateTransform((float)delta);
    }

    private void HandleKeyboardInput(float delta)
    {
        // Using Godot's default UI actions (Arrow keys / WASD if mapped)
        // You can replace "ui_left" with your own Input Map actions.
        float hInput = Input.GetAxis("ui_left", "ui_right"); // -1 left, +1 right
        float vInput = Input.GetAxis("ui_down", "ui_up");    // -1 down, +1 up

        if (Mathf.Abs(hInput) > 0.01f)
        {
            _yaw += hInput * KeySensitivity * 60f * delta;
        }

        if (Mathf.Abs(vInput) > 0.01f)
        {
            // Note: pressing "up" usually means looking up, which is negative X rotation
            float pitchChange = vInput * KeySensitivity * 60f * delta;
            
            if (InvertY) _pitch -= pitchChange;
            else _pitch += pitchChange; // Looks up when pressing up

            ClampPitch();
        }

        // Explicit WASD controls let the globe be panned without requiring
        // custom Input Map actions. Movement speeds up as the camera zooms out.
        float panHorizontal = (Input.IsKeyPressed(Key.D) ? 1.0f : 0.0f) -
                              (Input.IsKeyPressed(Key.A) ? 1.0f : 0.0f);
        float panVertical = (Input.IsKeyPressed(Key.S) ? 1.0f : 0.0f) -
                            (Input.IsKeyPressed(Key.W) ? 1.0f : 0.0f);

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
        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
    }

    private void UpdateTransform(float delta)
    {

        Vector3 targetRotation = new Vector3(Mathf.DegToRad(_pitch), Mathf.DegToRad(_yaw), 0);

        if (UseSmoothing)
        {
            // Interpolate the rotation quaternion for smoothness
            Quaternion currentQ = Quaternion.FromEuler(Rotation);
            Quaternion targetQ = Quaternion.FromEuler(targetRotation);
            Rotation = currentQ.Slerp(targetQ, SmoothSpeed * delta).GetEuler();
        }
        else
        {
            Rotation = targetRotation;
        }


        if (_camera != null)
        {
            Vector3 camPos = _camera.Position;
            if (UseSmoothing)
            {
                camPos.Z = Mathf.Lerp(camPos.Z, _targetDistance, SmoothSpeed * delta);
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

	    Vector3 dir = cell.Center.Normalized();


	    float targetYawRad = Mathf.Atan2(dir.X, dir.Z);
	    float targetYawDeg = Mathf.RadToDeg(targetYawRad);


	    float targetPitchRad = Mathf.Asin(dir.Y);
	    float targetPitchDeg = -Mathf.RadToDeg(targetPitchRad);
	    
	    _yaw = Mathf.LerpAngle(_yaw, targetYawDeg, 1.0f); 
    
	    _yaw = targetYawDeg;
	    _pitch = targetPitchDeg;
	    
	    if (optionalZoom.HasValue)
	    {
		    _targetDistance = Mathf.Clamp(optionalZoom.Value, MinZoom, MaxZoom);
	    }

	    ClampPitch();
    }
    
    public void FocusOnCell(int cellIndex, float? optionalZoom = null)
    {
	    HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromIndex(cellIndex);
	    
	    if (cell == null) return;
	    

	    Vector3 dir = cell.Value.Center.Normalized();


	    float targetYawRad = Mathf.Atan2(dir.X, dir.Z);
	    float targetYawDeg = Mathf.RadToDeg(targetYawRad);


	    float targetPitchRad = Mathf.Asin(dir.Y);
	    float targetPitchDeg = -Mathf.RadToDeg(targetPitchRad);
	    
	    _yaw = Mathf.LerpAngle(_yaw, targetYawDeg, 1.0f); 
    
	    _yaw = targetYawDeg;
	    _pitch = targetPitchDeg;
	    
	    if (optionalZoom.HasValue)
	    {
		    _targetDistance = Mathf.Clamp(optionalZoom.Value, MinZoom, MaxZoom);
	    }

	    ClampPitch();
    }
}
