using Godot;
using System;

[GlobalClass]
public partial class OrbitalCamera : Node3D
{
	public static OrbitalCamera Instance;
    [ExportGroup("Settings")]
    [Export] public float MouseSensitivity = 0.3f;
    [Export] public float KeySensitivity = 2.0f; // Speed for keyboard rotation
    [Export] public bool InvertY = false;
    [Export] public float ScrollSpeed = 2.0f;
    [Export] public bool UseSmoothing = true;
    [Export] public float SmoothSpeed = 10.0f;

    [ExportGroup("Limits")]
    [Export] public float MinPitch = -89.0f; // Prevent looking straight up/flipping
    [Export] public float MaxPitch = 89.0f;  // Prevent looking straight down
    [Export] public float MinZoom = 2.0f;
    [Export] public float MaxZoom = 20.0f;
	
    // Current logical rotation (degrees)
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
        _targetDistance = _camera.Position.Z;
        Instance = this;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // 1. Mouse Rotation (Only when Right Click is held)
        if (@event is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _yaw -= mouseMotion.Relative.X * MouseSensitivity;
            
            float pitchDelta = mouseMotion.Relative.Y * MouseSensitivity;
            if (InvertY) _pitch -= pitchDelta;
            else _pitch -= pitchDelta;

            ClampPitch();
        }

        // 2. Mouse Zoom (Scroll Wheel)
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
    }

    private void ClampPitch()
    {
        // This is the specific logic to handle the "Globe Poles"
        // We strictly limit the angle so it never hits 90 or -90 degrees.
        // This prevents the camera from flipping upside down (Gimbal Lock).
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