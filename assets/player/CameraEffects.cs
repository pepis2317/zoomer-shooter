using Godot;
using System;

public partial class CameraEffects : Camera3D
{
	[Export] public PlayerController PlayerController;
	[Export] public bool EnableTilt = true;
	[Export] public bool EnableFallKick = true;
	[Export] public bool EnableDamageKick = true;
	[Export] public bool EnableWeaponKick = true;
	[Export] public bool EnableHeadBob = true;

	[Export] public float RunPitch = 0.1f;
	[Export] public float MaxPitch = 1f;
	[Export] public float FallTime = 0.3f;
	[Export] public float DamageTime = 0.3f;
	[Export] public float WeaponDecay = 0.5f;
	[Export(PropertyHint.Range, "0.0,0.1,0.001")] public float BobPitch = 0.05f;
	[Export(PropertyHint.Range, "0.0,0.1,0.001")] public float BobRoll = 0.025f;
	[Export(PropertyHint.Range, "0.0,0.04,0.001")] public float BobUp = 0.005f;
	[Export(PropertyHint.Range, "3.0,8.0,0.1")] public float BobFrequency = 6f;

	private float _stepTimer = 0;

	private float _fallValue = 0;
	private float _fallTimer = 0;

	private float _damagePitch = 0;
	private float _damageRoll = 0;
	private float _damageTimer = 0;
	private Vector3 _weaponKickAngles = Vector3.Zero;

	public void CalculateViewOffset(float delta)
	{
		if (PlayerController == null)
		{
			return;
		}
		_fallTimer -= delta;
		_damageTimer -= delta;
		var velocity = PlayerController.Velocity;

		var speed = new Vector2(velocity.X, velocity.Z).Length();
		if (speed > 0.1 && PlayerController.IsOnFloor())
		{
			_stepTimer += delta * (speed / BobFrequency);
			_stepTimer %= 1f;
		}
		else
		{
			_stepTimer = 0;
		}
		var bobSin = Mathf.Sin(_stepTimer * 2 * Mathf.Pi) * 0.5f;

		var angles = Vector3.Zero;
		var offset = Vector3.Zero;
		if (EnableTilt)
		{
			var right = GlobalTransform.Basis.X;

			var rightDot = velocity.Dot(right);
			var sideTilt = Mathf.Clamp(
				rightDot * Mathf.DegToRad(RunPitch),
				Mathf.DegToRad(-MaxPitch),
				Mathf.DegToRad(MaxPitch)
			);
			angles.Z -= sideTilt;
		}
		if (EnableFallKick)
		{
			var fallRatio = Mathf.Max(0, _fallTimer / FallTime);
			var fallKickAmount = fallRatio * _fallValue;
			angles.X -= fallKickAmount;
			offset.Y -= fallKickAmount;
		}
		if (EnableDamageKick)
		{
			var damageRatio = Mathf.Max(0, _damageTimer / DamageTime);
			angles.X += damageRatio * _damagePitch;
			angles.Z += damageRatio * _damageRoll;
		}
		if (EnableWeaponKick)
		{
			_weaponKickAngles = _weaponKickAngles.MoveToward(Vector3.Zero, WeaponDecay * delta);
			angles += _weaponKickAngles;
		}
		if (EnableHeadBob)
		{
			var pitchDelta = bobSin * Mathf.DegToRad(BobPitch) * speed;
			angles.X -= pitchDelta;

			var rollDelta = bobSin * Mathf.DegToRad(BobRoll) * speed;
			angles.Z -= rollDelta;

			var bobHeight = bobSin * speed * BobUp;
			offset.Y += bobHeight;
		}
		Position = offset;
		Rotation = angles;

	}

	public void AddFallKick(float strength)
	{
		_fallValue = Mathf.DegToRad(strength);
		_fallTimer = FallTime;
	}

	public void AddDamageKick(float pitch, float roll, Vector3 source)
	{
		var forward = GlobalTransform.Basis.Z;
		var right = GlobalTransform.Basis.X;
		var direction = GlobalPosition.DirectionTo(source);
		var forwardDot = direction.Dot(forward);
		var rightDot = direction.Dot(right);
		_damagePitch = Mathf.DegToRad(pitch) * forwardDot;
		_damageRoll = Mathf.DegToRad(roll) * rightDot;
		_damageTimer = DamageTime;
	}

	public void AddWeaponKick(float pitch, float yaw, float roll)
	{
		Random rand = new Random();
		_weaponKickAngles.X = Mathf.Clamp(_weaponKickAngles.X + Mathf.DegToRad(pitch), -MaxPitch, MaxPitch);
		float randomYawFloat = (float)(-yaw + (rand.NextDouble() * (yaw * 2)));
		_weaponKickAngles.Y += Mathf.DegToRad(randomYawFloat);
		float randomRollFloat = (float)(-roll + (rand.NextDouble() * (roll * 2)));
		_weaponKickAngles.Z += Mathf.DegToRad(randomRollFloat);
	}

	public override void _Process(double delta)
	{
		CalculateViewOffset((float)delta);
	}
}
