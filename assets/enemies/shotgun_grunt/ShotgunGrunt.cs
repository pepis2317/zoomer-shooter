using Godot;

public partial class ShotgunGrunt : Enemy
{
	[Export] public float MoveSpeed = 12f;
	[Export] public float RoamDistance = 5f;
	[Export] public float RoamRadius = 1f;
	[Export] public float RoamSpeed = 8f;
	[Export] public float RoamJitter = 0.5f;
	public bool IsStaggered = false;
	public bool IsRoaming = false;
	public float EnemyDeceleration;
	private float _roamAngle;
	public override void _Ready()
	{
		_outsideRange = new SGOutsideRangeState();
		_insideRange = new SGInsideRangeState();
		base._Ready();
	}
	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
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
