using Godot;
using System;

public partial class EnemySpawner : Marker3D
{
	[ExportCategory("References")]
	[Export] public MeshInstance3D Mesh;
	[Export] public string Id;

	[ExportCategory("Spawn Zone")]
	[Export] public bool IgnoreZones;
	[Export] public string ZoneId;

	[ExportCategory("Misc")]
	[Export] public float Height = 2;

	public Node3D Target;
	public float? Health;

	public override void _Ready()
	{
		Mesh.Hide();
		AddToGroup("EnemySpawner");
	}

	public void SpawnEnemy(Node parent)
	{
		if (ZoneId == null && !IgnoreZones)
		{
			GD.Print("zone not set");
			return;
		}
		if (Id == null)
		{
			GD.Print("enemy id not set");
			return;
		}
		var groundPos = FindGroundPosition(GlobalPosition, Height);
		var transform = new Transform3D(GlobalTransform.Basis, groundPos);

		var enemy = EnemyPools.Instance.Acquire(Id, transform, Health, ZoneId);
		parent.AddChild(enemy);
		EnemyPools.Instance.CallDeferred(EnemyPools.MethodName.ReleaseSpawner, Id, this);
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

		GD.PushWarning("EnemySpawner: no ground found below spawn point, using raw marker position");
		return origin;
	}
}
