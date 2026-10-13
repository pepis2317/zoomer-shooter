using Godot;

public abstract partial class Projectile : Node3D
{
	[Signal] public delegate void DespawnProjectileEventHandler();
	[Export] CollisionArea CollisionArea;
	public Vector3 Velocity;
	public Node3D Source;
	public float Damage;
	public float KnockbackForce;
	public bool HasHit = false;
	private float _freeTimer = 0f;

	protected virtual void OnBodyEntered(CollisionArea body)
	{
		if (HasHit || body.GetParent() == Source) return;
		HasHit = true;
	}
	protected void ApplyDamage(Node3D target)
	{
		var healthComponent = target.GetNodeOrNull<Health>("Health");
		if (healthComponent != null)
		{
			Vector3 direction = (target.GlobalPosition - GlobalPosition).Normalized();
			healthComponent.TakeDamage(Damage, KnockbackForce, Source, direction);
		}
		Velocity = Vector3.Zero;
		EmitSignal(SignalName.DespawnProjectile);
	}
	public void Setup(Vector3 val, float dmg, float force, Node3D source)
	{
		Velocity = val;
		Damage = dmg;
		KnockbackForce = force;
		Source = source;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_freeTimer > 2f)
		{
			EmitSignal(SignalName.DespawnProjectile);
			return;
		}
		GlobalPosition += Velocity * (float)delta;
		_freeTimer += (float)delta;
	}
	public void Activate()
	{
		Show();
		// AabbCollisionManager.Instance.Register(CollisionArea);
		ProcessMode = ProcessModeEnum.Inherit;
		CollisionArea.ResetSweep();
		GpuCollisionManager.Instance.Register(CollisionArea);
		CollisionArea.BodyEntered += OnBodyEntered;
	}
	public void Deactivate()
	{
		Hide();
		_freeTimer = 0f;
		Velocity = Vector3.Zero;
		Damage = 0;
		HasHit = false;
		Source = null;
		KnockbackForce = 0;
		// AabbCollisionManager.Instance.UnRegister(CollisionArea);
		GpuCollisionManager.Instance.UnRegister(CollisionArea);
		CollisionArea.BodyEntered -= OnBodyEntered;
		ProcessMode= ProcessModeEnum.Disabled;
	}
}
