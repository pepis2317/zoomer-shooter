using Godot;

public partial class ShotgunGrunt : Enemy
{

	[ExportCategory("Movement Settings")]
	[Export] public float RoamDistance = 5f;
	[Export] public float RoamRadius = 1f;
	[Export] public float RoamSpeed = 8f;
	[Export] public float RoamJitter = 0.5f;
	[Export] public float RepositionCooldown = 3f;
	[Export] public float RepositionBoost = 10f;

	[ExportCategory("Weapon Settings")]
	[Export] public PackedScene ProjectileScene;
	[Export] public float Accuracy = 20f;
	[Export] public int PelletCount = 4;
	[Export] public float FiringDistance = 5f;
	[Export] public float ProjectileSpeed = 50f;
	[Export] public float FireCooldown = 3f;
	[Export] public float SpreadAngle = 20f;
	[Export] public float Damage = 5f;
	[Export] public float KnockbackForce = 5f;
	[Export] public int AttackCost = 10;

	public PhysicsRayQueryParameters3D RangeQuery;
	public bool IsStaggered = false;
	public bool IsRoaming = false;
	public float EnemyDeceleration;
	public float FireTimer = 0f;
	private float _roamAngle;
	private bool _hasReturnedToken = true;


	public override void _Ready()
	{
		ProjectilePools.Instance.RegisterProjectiles(ProjectileScene, PelletCount);
		RangeQuery = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition + Vector3.Forward * FiringDistance, GeometryCollisionMask);
		_outsideRange = new SGOutsideRangeState();
		_insideRange = new SGInsideRangeState();
		base._Ready();
	}
	private void SpawnProjectile()
	{
		var projectile = ProjectilePools.Instance.Acquire(ProjectileScene);
		if (projectile == null)
		{
			return;
		}
		GetTree().CurrentScene.AddChild(projectile);
		var accuracySpread = (100 - Accuracy) / 1000f;

		var toTarget = (Target.GlobalTransform.Origin - GlobalTransform.Origin).Normalized();
		var worldUp = Vector3.Up;
		var right = toTarget.Cross(worldUp).Normalized();
		var up = right.Cross(toTarget).Normalized();

		var rng = new RandomNumberGenerator();
		var accuracyX = rng.RandfRange(-accuracySpread, accuracySpread);
		var accuracyY = rng.RandfRange(-accuracySpread, accuracySpread);
		var spread = right * accuracyX + up * accuracyY;
		var direction = (toTarget + spread).Normalized();
		if (PelletCount > 1)
		{
			var spreadRad = Mathf.DegToRad(SpreadAngle);
			var spreadX = rng.RandfRange(-spreadRad, spreadRad);
			var spreadY = rng.RandfRange(-spreadRad, spreadRad);
			var rotX = new Quaternion(right, spreadX);
			var rotY = new Quaternion(up, spreadY);
			direction = (rotX * rotY * direction).Normalized();
		}
		projectile.GlobalPosition = GlobalPosition + GlobalTransform.Basis * Vector3.Forward * 2;
		var velocity = direction * ProjectileSpeed;
		var upVector = Mathf.Abs(direction.Dot(Vector3.Up)) < 0.999f ? Vector3.Up : Vector3.Right;
		projectile.LookAt(projectile.GlobalPosition + direction, upVector);
		projectile.Setup(velocity, Damage, KnockbackForce, this);
	}

	public void FireWeapon()
	{
		for (int i = 0; i < PelletCount; i++)
		{
			SpawnProjectile();
		}
		FireTimer = FireCooldown;
		_hasReturnedToken = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (FireTimer > 0)
		{
			FireTimer -= (float)delta;
		}
		else
		{
			if (!_hasReturnedToken)
			{
				AttackTokenManager.Instance.ReturnToken(AttackCost);
				_hasReturnedToken = true;
			}
		}

		if (IsInstanceValid(Target))
		{
			Vector3 lookDirection = Target.GlobalPosition - GlobalPosition;
			lookDirection.Y = 0;
			if (lookDirection.LengthSquared() > 0.0001f)
			{
				Basis newBasis = Basis.LookingAt(lookDirection, Vector3.Up);
				Transform = new Transform3D(newBasis, Transform.Origin);
			}
		}
	}
}
