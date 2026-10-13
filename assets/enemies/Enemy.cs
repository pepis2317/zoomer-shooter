using Godot;
using System;

public abstract partial class Enemy : Node3D, INavigationTickable
{
	[Signal] public delegate void DespawnEnemyEventHandler();

	[ExportCategory("References")]
	[Export] public NavigationAgent3D NavigationAgent;
	[Export] public Health Health;
	[Export] public Stamina Stamina;
	[Export] public CollisionArea CollisionArea;
	[Export] public string Id;

	[ExportCategory("Groups")]
	[Export] public string[] EnemyGroups = Array.Empty<string>();
	[ExportCategory("Settings")]
	[Export] public float MoveSpeed = 12f;
	[Export] public float KnockbackFriction = 5f;
	[Export] public float FloorCheckDistance = 2f;
	[Export] public uint GeometryCollisionMask = 1;
	[Export] public float CeilingCheckDistance = 1f;
	[Export] public float WallCheckDistance = 0.5f;
	[Export] public float EffectiveRange = 10f;
	[Export] public float Acceleration = 5f;
	[Export] public float Deceleration = 5f;
	[Export] public float StopSpeed = 2f;
	[Export] public float ExplosionDamage = 15f;
	[Export] public float ExplosionKnockbackForce = 25f;
	[Export] public float PushStrength = 1f;

	private PhysicsRayQueryParameters3D _query;
	private Vector3 _externalVelocity;
	private Vector3[] _sideDirs = { Vector3.Forward, Vector3.Back, Vector3.Left, Vector3.Right };
	private float _gravity;
	private float _minFloorDot = Mathf.Cos(Mathf.DegToRad(45f));
	protected StateMachine _rootState = new RootState();
	protected StateMachine _unstaggeredState = new UnstaggeredState();
	protected StateMachine _insideRange = new InsideRangeState();
	protected StateMachine _outsideRange = new OutsideRangeState();
	protected StateMachine _staggeredState = new StaggeredState();

	public Vector3 Velocity;
	public int SkipCounter = 0;
	public Vector3 RepositionDirection;
	public Vector3 RepositionPoint;
	public Vector3 ExternalVelocity
	{
		get => _externalVelocity;
		set
		{
			_externalVelocity = value;
		}
	}
	public Node3D Target;
	public string ZoneId;
	public bool IsRepositioning = false;

	public override void _Ready()
	{
		_query = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition + Vector3.Down * FloorCheckDistance, GeometryCollisionMask);
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		foreach (var group in EnemyGroups)
		{
			AddToGroup(group);
		}
		_rootState.Node = this;
		_unstaggeredState.Node = this;
		_staggeredState.Node = this;
		_insideRange.Node = this;
		_outsideRange.Node = this;

		_rootState.LoadSubState(_unstaggeredState);
		_rootState.LoadSubState(_staggeredState);

		_unstaggeredState.LoadSubState(_outsideRange);
		_unstaggeredState.LoadSubState(_insideRange);

		_rootState.AddTransition(_unstaggeredState, _staggeredState, EnemyStateTriggers.STAGGERED);
		_rootState.AddTransition(_staggeredState, _unstaggeredState, EnemyStateTriggers.UNSTAGGERED);

		_unstaggeredState.AddTransition(_outsideRange, _insideRange, EnemyStateTriggers.INSIDERANGE);
		_unstaggeredState.AddTransition(_insideRange, _outsideRange, EnemyStateTriggers.OUTSIDERANGE);

		_rootState.EnterStateMachine();
	}

	private void OnBodyEntered(CollisionArea body)
	{
		if (body.GetParent() is not Enemy enemy)
		{
			return;
		}
		IsRepositioning = false;
		var away = GlobalPosition - enemy.GlobalPosition;
		away.Y = 0f;
		var push = away.Normalized() * PushStrength;

		ExternalVelocity += push;
		enemy.ExternalVelocity -= push;
	}

	private void OnStaminaDamageTaken(float amount)
	{
		GD.Print("stamina damage taken");
	}

	private void OnDamageTaken(float amount, Node3D source, Vector3 direction, float knockbackForce, Vector3 position)
	{
		GD.Print("damage taken");
	}

	public bool IsOnFloor()
	{
		var space = GetWorld3D().DirectSpaceState;
		Vector3 pos = GlobalPosition;
		_query.From = pos;
		_query.To = pos + Vector3.Down * FloorCheckDistance;

		var result = space.IntersectRay(_query);
		if (result.Count == 0) return false;

		Vector3 normal = (Vector3)result["normal"];
		if (normal.Dot(Vector3.Up) <= _minFloorDot) return false;

		if (Velocity.Y <= 0f)
		{
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
		}
		return true;
	}

	public bool IsOnCeiling()
	{
		var space = GetWorld3D().DirectSpaceState;
		Vector3 pos = GlobalPosition;
		_query.From = pos;
		_query.To = pos + Vector3.Up * CeilingCheckDistance;
		var result = space.IntersectRay(_query);
		if (result.Count == 0) return false;
		return true;
	}

	public bool IsOnWall(out Vector3 wallNormal)
	{
		wallNormal = Vector3.Zero;
		var spaceState = GetWorld3D().DirectSpaceState;
		Vector3 pos = GlobalPosition;
		float closest = float.MaxValue;
		bool found = false;
		foreach (var localDir in _sideDirs)
		{
			Vector3 dir = GlobalBasis * localDir;
			dir.Y = 0;
			dir = dir.Normalized();
			_query.From = pos;
			_query.To = pos + dir * WallCheckDistance;
			var result = spaceState.IntersectRay(_query);
			if (result.Count == 0) continue;
			var normal = result["normal"].As<Vector3>();
			if (Mathf.Abs(normal.Y) > 0.1f) continue;
			float distSq = pos.DistanceSquaredTo(result["position"].As<Vector3>());
			if (distSq < closest)
			{
				closest = distSq;
				wallNormal = normal;
				found = true;
			}
		}
		return found;
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
		var friction = Deceleration;
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
		if (Velocity > Vector3.Zero)
		{
			IsRepositioning = true;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Velocity += _externalVelocity;
		_externalVelocity = Vector3.Zero;
		GlobalPosition += Velocity * (float)delta;
		if (!IsOnFloor())
		{
			Velocity += _gravity * Vector3.Down * (float)delta;
		}
		if (IsOnCeiling())
		{
			Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
		}
		if (IsOnWall(out var wallNormal))
		{
			Velocity = Velocity.Slide(wallNormal);
		}
		if (!IsRepositioning)
		{
			PMFriction((float)delta);
		}
		_unstaggeredState.UpdateStateMachine((float)delta);
	}

	public void Activate()
	{
		Velocity = Vector3.Zero;
		Show();
		ProcessMode = ProcessModeEnum.Inherit;
		// AabbCollisionManager.Instance.Register(CollisionArea);
		GpuCollisionManager.Instance.Register(CollisionArea);
		Health.DamageTaken += OnDamageTaken;
		Stamina.StaminaDamageTaken += OnStaminaDamageTaken;
		CollisionArea.BodyEntered += OnBodyEntered;
	}

	public void Deactivate()
	{
		Hide();
		_unstaggeredState.ExitStateMachine();
		// AabbCollisionManager.Instance.UnRegister(CollisionArea);
		GpuCollisionManager.Instance.UnRegister(CollisionArea);
		EnemyNavigationManager.Instance.UnRegister(this);
		Health.DamageTaken -= OnDamageTaken;
		Stamina.StaminaDamageTaken -= OnStaminaDamageTaken;
		CollisionArea.BodyEntered -= OnBodyEntered;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void PlaceSpawner()
	{
		if (Health.CurrentHealth <= 0) return;
		var spawner = EnemyPools.Instance.AcquireSpawner(Id, Transform, Health.CurrentHealth, ZoneId);
		if (spawner == null) return;
		GetParent().AddChild(spawner);
		EmitSignal(SignalName.DespawnEnemy);
	}

	public void NavigationTick()
	{
		if (!IsInstanceValid(Target))
		{
			return;
		}

		var distSquared = GlobalPosition.DistanceSquaredTo(Target.GlobalPosition);
		var farDist = EnemyNavigationManager.Instance.FarDistance;
		var veryFarDist = EnemyNavigationManager.Instance.VeryFarDistance;
		var farDistSquared = farDist * farDist;
		var veryFarDistSquared = veryFarDist * veryFarDist;

		if (distSquared > farDistSquared && (SkipCounter++ % 2) != 0)
		{
			return;
		}
		if (distSquared > veryFarDistSquared && (SkipCounter++ % 4) != 0)
		{
			return;
		}
		var toEnemy = GlobalPosition - Target.GlobalPosition;
		toEnemy.Y = 0;
		var dir = toEnemy.LengthSquared() > 0.0001f ? toEnemy.Normalized() : Vector3.Forward;
		Vector3 point = Target.GlobalPosition + dir * EffectiveRange;
		InitReposition(point);
	}

	public void InitReposition(Vector3 position)
	{
		IsRepositioning = true;
		NavigationAgent.TargetPosition = position;
		RepositionDirection = RepositionPoint - GlobalPosition;
	}

	public void HandleFollowNavigation(float delta)
	{
		if (NavigationAgent.IsNavigationFinished())
		{
			Velocity = Vector3.Zero;
			NavigationAgent.Velocity = Vector3.Zero;
			IsRepositioning = false;
			return;
		}

		var nextPos = NavigationAgent.GetNextPathPosition();
		var direction = nextPos - GlobalPosition;
		direction.Y = 0;
		direction = direction.Normalized();

		var desiredVelocity = direction * MoveSpeed;
		Velocity = Velocity.MoveToward(desiredVelocity, Acceleration * delta);
	}
}
