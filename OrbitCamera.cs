using Godot;

public partial class OrbitCamera : Node3D
{
    // Center of the 5x20x5 board: X = 2.0, Y = 9.5, Z = 2.0
    [Export] public Vector3 TargetPosition = new(2.0f, 9.5f, 2.0f);

    [Export] public float Distance = 22.0f;
    [Export] public float MouseSensitivity = 0.005f;
    [Export] public float KeyboardSpeed = 2.0f;

    [Export] public float MinPitchDegrees = -80.0f;
    [Export] public float MaxPitchDegrees = 80.0f;
    [Export] public float MinDistance = 8.0f;
    [Export] public float MaxDistance = 45.0f;

    private float _yaw = 0.0f;
    private float _pitch = 0.0f;
    private bool _isDragging = false;
    private Camera3D _camera;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        Position = TargetPosition;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Right-Click Drag to Orbit
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isDragging = mouseButton.Pressed;
            }
            // Mouse Wheel Zoom
            else if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                Distance = Mathf.Clamp(Distance - 1.5f, MinDistance, MaxDistance);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                Distance = Mathf.Clamp(Distance + 1.5f, MinDistance, MaxDistance);
            }
        }

        // Mouse Motion Tracking
        if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            _yaw -= mouseMotion.Relative.X * MouseSensitivity;
            _pitch -= mouseMotion.Relative.Y * MouseSensitivity;

            ClampPitch();
        }
    }

    public override void _Process(double delta)
    {
        float floatDelta = (float)delta;

        // Keyboard Controls (A / D or Left / Right to orbit Yaw)
        if (Input.IsKeyPressed(Key.A)) _yaw += KeyboardSpeed * floatDelta;
        if (Input.IsKeyPressed(Key.D)) _yaw -= KeyboardSpeed * floatDelta;

        // Keyboard Controls (W / S or Up / Down to orbit Pitch)
        if (Input.IsKeyPressed(Key.W)) _pitch += KeyboardSpeed * floatDelta;
        if (Input.IsKeyPressed(Key.S)) _pitch -= KeyboardSpeed * floatDelta;

        ClampPitch();

        // Apply 3D rotation to the Pivot
        Rotation = new Vector3(_pitch, _yaw, 0.0f);

        // Keep the pivot centered and update camera distance offset
        Position = TargetPosition;
        if (_camera != null)
        {
            _camera.Position = new Vector3(0.0f, 0.0f, Distance);
        }
    }

    private void ClampPitch()
    {
        float minRad = Mathf.DegToRad(MinPitchDegrees);
        float maxRad = Mathf.DegToRad(MaxPitchDegrees);
        _pitch = Mathf.Clamp(_pitch, minRad, maxRad);
    }
}
