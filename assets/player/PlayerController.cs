using Godot;

public partial class PlayerController : CharacterBody3D
{
	[ExportCategory("References")]
	[Export] public CollisionShape3D CollisionShape;
	[Export] public RayCast3D EdgeFrictionRayCast;
	[Export] public CameraController CameraController;

	[ExportCategory("Movement Settings")]
	[Export] public float JumpVelocity = 3.7f;
	[Export] public float Acceleration = 5f;
	[Export] public float Speed = 12f;
	[Export] public float Deceleration = 0.5f;
	[Export] public float EdgeDeceleration = 6f;
	[Export] public float AirAcceleration = 8f;
	[Export] public float StopSpeed = 0.5f;
	
	[ExportCategory("Misc")]
	[Export] public float Height = 1.65f;

	public Vector2 InputDirection;
	private float _playerDeceleration;

	public void PMAccelerate(Vector3 wishDir, float wishSpeed, float accel, float delta)
	{
		var currentSpeed = Velocity.Dot(wishDir);
		var addSpeed = wishSpeed - currentSpeed;
		if (addSpeed <= 0)
		{
			return;
		}
		var accelSpeed = accel * wishSpeed * delta;
		if (accelSpeed > addSpeed)
		{
			accelSpeed = addSpeed;
		}
		Velocity += accelSpeed * wishDir;
	}

	public void PMFriction(float delta)
	{
		var vel = Velocity;
		var speed = Mathf.Sqrt(vel.X * vel.X + vel.Y * vel.Y + vel.Z * vel.Z);
		if (speed < 0.1)
		{
			vel.X = 0;
			vel.Z = 0;
			Velocity = vel;
			return;
		}
		var friction = _playerDeceleration;
		float drop = 0;
		if (IsOnFloor())
		{
			var control = speed < StopSpeed ? StopSpeed : speed;
			drop += control * friction * delta;
		}
		var newSpeed = speed - drop;
		if (newSpeed < 0)
		{
			newSpeed = 0;
		}
		newSpeed /= speed;
		vel *= newSpeed;
		Velocity = vel;
	}
	
	public void SetDeceleration(float deceleration)
	{
		_playerDeceleration = deceleration;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsOnFloor())
		{
			Velocity += GetGravity() * (float)delta;
		}
		if(IsOnFloor() && Input.IsActionJustPressed("jump"))
		{
			Jump();
		}
		PMFriction((float)delta);
		InputDirection = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		var direction = (Transform.Basis * new Vector3(InputDirection.X, 0, InputDirection.Y)).Normalized();
		var accel = IsOnFloor() ? Acceleration : AirAcceleration;
		PMAccelerate(direction, Speed, accel, (float)delta);
		MoveAndSlide();
	}

	public void UpdateRotation(Vector3 rotationInput)
	{
		Transform3D globalTransform = GlobalTransform;
		globalTransform.Basis = Basis.FromEuler(rotationInput);
		GlobalTransform = globalTransform;
	}

	 public void Jump()
    {
        Vector3 velocity = Velocity;
        velocity.Y += JumpVelocity;
        Velocity = velocity;
    }
}