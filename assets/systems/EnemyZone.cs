using Godot;

public partial class EnemyZone : Area3D
{
	[Export] public string ZoneId;
	public override void _Ready()
	{
		AddToGroup("EnemyZone");
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if(body is PlayerController player)
		{
			GD.Print("entered: ", ZoneId);
			ZoneManager.Instance.ChangeZone(GetParent(), ZoneId, player);
		}
	}
	public override void _ExitTree()
	{
		BodyEntered -= OnBodyEntered;
	}
}
