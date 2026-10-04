using Godot;

public partial class PlayerSpawner : Marker3D
{
	[Export] public PackedScene PlayerScene;
	[Export] public MeshInstance3D Mesh;
	public override void _Ready()
	{
		AddToGroup("PlayerSpawner");
		Mesh.Hide();
	}
	public void SpawnPlayer(Node parent)
	{
		var player = PlayerScene.Instantiate<PlayerController>();
		parent.AddChild(player);
		var spawnPos = GlobalTransform.Origin;
		var groundPos = FindGroundPosition(spawnPos, player.Height);
		player.GlobalPosition = groundPos;
		player.CameraController.SetLook(0, GlobalRotation.Y);
	}
	private Vector3 FindGroundPosition(Vector3 origin, float capsuleHeight)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		Vector3 rayStart = origin + Vector3.Up * 2f;   // start a bit above the marker
		Vector3 rayEnd = origin + Vector3.Down * 10f;  // cast well below it

		var query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
		query.CollideWithAreas = false;

		var result = spaceState.IntersectRay(query);
		if (result.Count > 0)
		{
			Vector3 hitPoint = (Vector3)result["position"];
			return hitPoint + Vector3.Up * (capsuleHeight / 2f); // lift by half capsule so feet sit on the hit point
		}

		GD.PushWarning("PlayerSpawner: no ground found below spawn point, using raw marker position");
		return origin;
	}
}
