using System;
using FirstArrival.Scripts.Managers;
using FirstArrival.Scripts.Utility;
using Godot;

[GlobalClass]
public partial class CameraController : Node3D
{
    public static CameraController Instance;
    #region Variables
	[Export] public Camera3D MainCamera {get; private set;}
    [Export] private Vector3 lookAtOffset;
    [Export] private float moveSpeed;
    [Export] private float zoomSpeed;
    [Export] private float rotationSpeed;

    #endregion

    #region Events

    #endregion

    #region Functions
    private void Setup()
    {
	    GridObjectTeamHolder playerTeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
	    
	    playerTeamHolder.SelectedGridObjectChanged += GridObjectTeam_GridObjectSelected;
    }

    public override void _ExitTree()
    {
        GridObjectTeamHolder playerTeamHolder = GridObjectManager.Instance.GetGridObjectTeamHolder(Enums.UnitTeam.Player);
	    
        playerTeamHolder.SelectedGridObjectChanged -= GridObjectTeam_GridObjectSelected;
        base._ExitTree();
    }

    public void QuickSwitchTarget(Node3D target)
    {
        // float QuickSwitchSpeed = 150f;
        if (target == null) return;
        this.Position = target.Position;
    }
    private void TransposerMovement(float delta)
    {
        Vector3 moveDirection = Vector3.Zero;
        if (Input.IsActionPressed("cameraRight"))
        {
            moveDirection += -this.Basis.X * delta;
        }
        if (Input.IsActionPressed("cameraLeft"))
        {
            moveDirection += this.Basis.X * delta;
        }
        if (Input.IsActionPressed("cameraUp"))
        {
            moveDirection += this.Basis.Z * delta;
        }
        if (Input.IsActionPressed("cameraDown"))
        {
            moveDirection += -this.Basis.Z * delta;
        }

        this.Position += (moveDirection * moveSpeed);


        // var spaceState = GetWorld3D().DirectSpaceState;
        // var mousePosition = GetViewport().GetMousePosition();
        // var from = transposer.Position;
        // var to = from + (Vector3.Down * 25);

        // var query = PhysicsRayQueryParameters3D.Create(from, to);
        // query.CollideWithAreas = true;
        // query.CollideWithBodies = true;
        // query.HitBackFaces = true;
        // var result = spaceState.IntersectRay(query);

        // if (result != null && result.Count > 0)
        // {
        //     //hit something
        //     transposer.Position = result["position"].As<Vector3>();
        // }

    }

    private void CameraZoom(float delta)
    {
        float zoomDirection = 0;
        if (Input.IsActionJustPressed("cameraScrollUp")) // Detects single scroll up event
        {
            zoomDirection -= 1;
        }
        if (Input.IsActionJustPressed("cameraScrollDown")) // Detects single scroll down event
        {
            zoomDirection += 1;
        }

        if (zoomDirection != 0) // Only adjust if scrolling happened
        {
            this.Position += new Vector3(0, (zoomDirection * zoomSpeed) * delta, 0);
        }
    }

    private void TransformRotation(float delta)
    {
        float rotateDirection = 0;
        if (Input.IsActionPressed("cameraRotateRight")) // Detects single scroll up event
        {
            rotateDirection -= 1;
        }
        if (Input.IsActionPressed("cameraRotateLeft")) // Detects single scroll down event
        {
            rotateDirection += 1;
        }

        if (rotateDirection != 0) // Only adjust if scrolling happened
        {
	        this.RotateY(delta * (rotateDirection * rotationSpeed));
        }
    }
    #region Base Class Functions
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Instance = this;
        CallDeferred("Setup");
    }



    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (Input.IsKeyPressed(Key.F))
        {
            // QuickSwitchTarget(UnitManager.Instance.SelectedUnit);
        }
    }


    public override void _PhysicsProcess(double delta)
    {
        TransposerMovement((float)delta);
        CameraZoom((float)delta);
        TransformRotation((float)delta);

    }
    #endregion

    #region EventHandlers
    private void GridObjectTeam_GridObjectSelected( GridObject gridObject )
    {
        QuickSwitchTarget(gridObject);
    }
    #endregion

    #region Get/Set Functions

    #endregion
    #endregion
}
