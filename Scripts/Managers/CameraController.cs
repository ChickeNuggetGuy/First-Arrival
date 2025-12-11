using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot;
using PhantomCamera;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class CameraController : Manager<CameraController>
{
    #region Variables

    [Export] public Node3D transposer;
    
    private PhantomCamera3D _pcam;
    private PhantomCameraHost _pcamHost;
    [Export] public Camera3D MainCamera {get; private set;}
    [Export] private Vector3 lookAtOffset;
    [Export] private float moveSpeed;
    [Export] private float zoomSpeed;
    [Export] private float rotationSpeed;
    [Export] private float minDistanceToTarget = 2.0f;  // Minimum distance to transposer
    [Export] private float maxDistanceToTarget = 50.0f; // Maximum distance from transposer
    
    [Export] public int CurrentYLevel {get; private set;}

    
    private float _manualYaw = 0f;
    #endregion

    #region Signals
[Signal]public delegate void  CameraYLevelChangedEventHandler(CameraController cameraController);
    #endregion

    #region Functions

    public override void _ExitTree()
    {
        GridObjectTeamHolder playerTeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
        
        playerTeamHolder.SelectedGridObjectChanged -= GridObjectTeam_GridObjectSelected;
        base._ExitTree();
    }

    public void QuickSwitchTarget(Node3D target)
    {
        if (target == null) return;
        transposer.Position = target.Position;
    }
    
    private void TransposerMovement(float delta)
    {
        Vector3 moveDirection = Vector3.Zero;
        
        // Calculate forward and right vectors based on the current yaw
        float yaw = _manualYaw;
        Vector3 forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw)).Normalized();
        Vector3 right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw)).Normalized();

        if (Input.IsActionPressed("cameraRight"))
        {
            moveDirection += right * delta;
        }
        if (Input.IsActionPressed("cameraLeft"))
        {
            moveDirection -= right * delta;
        }
        if (Input.IsActionPressed("cameraUp"))
        {
            moveDirection += forward * delta;
        }
        if (Input.IsActionPressed("cameraDown"))
        {
            moveDirection -= forward * delta;
        }

        transposer.Position += (moveDirection * moveSpeed);
    }
    
    private void HandleCameraRotation(float delta)
    {
        float yawDelta = 0f;

        // Inverted Q/E rotation (Q now rotates right, E now rotates left)
        if (Input.IsActionPressed("cameraRotateRight")) // Assuming this is E
        {
            yawDelta -= rotationSpeed * delta; // Changed from += to -=
        }
        if (Input.IsActionPressed("cameraRotateLeft")) // Assuming this is Q
        {
            yawDelta += rotationSpeed * delta; // Changed from -= to +=
        }

        if (Mathf.Abs(yawDelta) > 0.001f)
        {
            _manualYaw += yawDelta;
            // Update camera position around transposer
            UpdateCameraPosition();
        }
    }
    
    private void UpdateCameraPosition()
    {
        if (_pcam != null)
        {
            // Get the current horizontal distance (XZ plane)
            var currentOffset = _pcam.FollowOffset;
            float horizontalDistance = new Vector2(currentOffset.X, currentOffset.Z).Length();
            
            // Calculate new position based on yaw around the transposer
            float yaw = _manualYaw;
            Vector3 newOffset = new Vector3(
                Mathf.Sin(yaw) * horizontalDistance,
                currentOffset.Y, // Keep the same Y offset
                Mathf.Cos(yaw) * horizontalDistance
            );
            
            _pcam.FollowOffset = newOffset;
        }
    }
    
    #region Base Class Functions
    
    public override void _PhysicsProcess(double delta)
    {
        TransposerMovement((float)delta);
        HandleCameraRotation((float)delta);
    }
    
    public override void _UnhandledInput(InputEvent @event)
    {
	    if (@event is InputEventKey { Pressed: true, Keycode: Key.F })
	    { 
		    QuickSwitchTarget(GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player).CurrentGridObject);   
	    }
	    
        if (@event is not InputEventMouseButton mb || !mb.Pressed)
            return;

        if (mb.ButtonIndex != MouseButton.WheelUp &&
            mb.ButtonIndex != MouseButton.WheelDown &&
            mb.ButtonIndex != MouseButton.WheelLeft &&
            mb.ButtonIndex != MouseButton.WheelRight)
            return;

        // Inverted mouse scroll directions
        int dir = 0;
        if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelLeft)
            dir = 1;  // Changed from -1 to 1
        if (mb.ButtonIndex == MouseButton.WheelDown || mb.ButtonIndex == MouseButton.WheelRight)
            dir = -1; // Changed from 1 to -1

        bool shiftHeld = mb.ShiftPressed || Input.IsKeyPressed(Key.Shift);

        if (shiftHeld)
        {
            var mtg = MeshTerrainGenerator.Instance;
            int maxY = mtg.GetMapCellSize().Y - 1;
            CurrentYLevel = Mathf.Clamp(
                (dir < 0) ? CurrentYLevel + 1 : CurrentYLevel - 1,
                0,
                maxY
            );
            float stepY = mtg.cellSize.Y;
            var pos = transposer.Position;
            pos.Y = CurrentYLevel * stepY;
            transposer.Position = pos;
            EmitSignal(SignalName.CameraYLevelChanged, this);
        }
        else
        {
            if (_pcam != null)
            {
                var currentOffset = _pcam.FollowOffset;
                // Calculate current horizontal distance (XZ plane)
                float currentHorizontalDistance = new Vector2(currentOffset.X, currentOffset.Z).Length();
                float amount = zoomSpeed > 0 ? zoomSpeed : 5.0f;
                
                // Calculate new distance (inverted controls)
                float newHorizontalDistance = currentHorizontalDistance - (dir * amount);
                
                // Clamp the new distance
                newHorizontalDistance = Mathf.Clamp(newHorizontalDistance, minDistanceToTarget, maxDistanceToTarget);
                
                // Scale the X and Z components while preserving the Y component and angular position
                if (currentHorizontalDistance > 0)
                {
                    float scale = newHorizontalDistance / currentHorizontalDistance;
                    Vector3 newOffset = new Vector3(
                        currentOffset.X * scale,
                        currentOffset.Y, // Keep Y component unchanged
                        currentOffset.Z * scale
                    );
                    _pcam.FollowOffset = newOffset;
                }
                else
                {
                    // If current distance is zero, set a default offset
                    _pcam.FollowOffset = new Vector3(0, currentOffset.Y, newHorizontalDistance);
                }
            }
        }
        GetViewport().SetInputAsHandled();
    }
    
    #endregion

    #region EventHandlers
    private void GridObjectTeam_GridObjectSelected( GridObject gridObject )
    {
        QuickSwitchTarget(gridObject);
    }
    #endregion

    #region Get/Set Functions

    public void SetCurrentYLevel(int yLevel)
    {
        CurrentYLevel = yLevel;
    }
    #endregion
    #endregion

    public override string GetManagerName() => "CameraManager";

    protected override Task _Setup()
    {
        GridObjectTeamHolder playerTeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
        
        playerTeamHolder.SelectedGridObjectChanged += GridObjectTeam_GridObjectSelected;
            
        return Task.CompletedTask;
    }

    protected override Task _Execute()
    {
        GD.Print("MainCamera:", MainCamera?.Name ?? "NULL");

        if (MainCamera != null)
        {
            var host = MainCamera.GetChild(0).AsPhantomCameraHost();

            if (host != null)
            {
                PhantomCamera3D pCam = GetNode<Node3D>("%PhantomCamera3D").AsPhantomCamera3D();
                GD.Print("PhantomCamera3D Found:" + pCam);
                _pcam = pCam;
                if (_pcam != null)
                {
                    _pcam.Priority = 40;
                }
            }
        }

        return Task.CompletedTask;
    }
    
    #region manager Data
    public override void Load(Godot.Collections.Dictionary<string,Variant> data)
    {
	    GD.Print("No data to transfer");
    }

    public override Godot.Collections.Dictionary<string,Variant> Save()
    {
	    return null;
    }
    #endregion
    
}