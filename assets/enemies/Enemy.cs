using Godot;
using System;

public abstract partial class Enemy : Node3D
{
	[Signal] public delegate void DespawnEnemyEventHandler();

	[ExportCategory("References")]
	[Export] public NavigationAgent3D NavigationAgent;
	[Export] public Health Health;
	[Export] public string Id;

	[ExportCategory("Groups")]
	[Export] public string[] EnemyGroups = Array.Empty<string>();

	[ExportCategory("Settings")]
	[Export] public float KnockbackFriction = 5f;
	[Export] public float FloorCheckDistance = 2f;
	[Export] public uint GeometryCollisionMask = 1;
	[Export] public float CeilingCheckDistance = 1f;
	[Export] public float WallCheckDistance = 0.5f;
	[Export] public float EffectiveRange = 10f;
	[Export] public float Acceleration = 5f;
	[Export] public float Deceleration = 5f;
	[Export] public float StopSpeed = 5f;
	[Export] public float ExplosionDamage = 15f;
	[Export] public float ExplosionKnockbackForce = 25f;
	[Export] public float PushStrength = 1f;

	private PhysicsRayQueryParameters3D _query;
	private Vector3 _externalVelocity;
	private Vector3[] _sideDirs = { Vector3.Forward, Vector3.Back, Vector3.Left, Vector3.Right };
	private float _gravity;
	private float _minFloorDot = Mathf.Cos(Mathf.DegToRad(45f));

	public Vector3 Velocity;
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
		Health.DamageTaken += OnDamageTaken;
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
	}

	public void Activate()
	{
		Velocity = Vector3.Zero;
		Show();
		ProcessMode = ProcessModeEnum.Inherit;
	}

	public void Deactivate()
	{
		Hide();
		Target = null;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void PlaceSpawner()
	{
		if (Health.CurrentHealth <= 0) return;
		var spawner = EnemyPools.Instance.AcquireSpawner(Id, Transform, Target, Health.CurrentHealth, ZoneId);
		GetParent().AddChild(spawner);
		EmitSignal(SignalName.DespawnEnemy);
	}
}
