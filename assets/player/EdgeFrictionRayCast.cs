using Godot;

public partial class EdgeFrictionRayCast : RayCast3D
{
	[Export] public PlayerController PlayerController;
	private bool _edgeChecked = false;
	public override void _PhysicsProcess(double delta)
	{
		var normalizedVel = PlayerController.Velocity.Normalized();
		Position = new Vector3(normalizedVel.X, Position.Y, normalizedVel.Z);
		if (!IsColliding() && PlayerController.IsOnFloor())
		{
			PlayerController.SetDeceleration(PlayerController.EdgeDeceleration);
		}
		else
		{
			PlayerController.SetDeceleration(PlayerController.Deceleration);
		}
	}
}
