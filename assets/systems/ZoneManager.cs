using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ZoneManager : Node
{
	public static ZoneManager Instance;
	public List<EnemyZone> Zones = new();
	private string CurrentZoneId;
	public override void _Ready()
	{
		Instance = this;
	}
	public void InitializeZones()
	{
		var zones = GetTree().GetNodesInGroup("EnemyZone").OfType<EnemyZone>();
		Zones.AddRange(zones);
	}
	public void ChangeZone(Node parent, string zoneId, Node3D target)
	{
		if (CurrentZoneId != zoneId || CurrentZoneId != null)
		{
			CurrentZoneId = zoneId;
			var enemiesOutsideOfZone = GetTree().GetNodesInGroup("Enemy").OfType<Enemy>().Where(q => q.ZoneId != zoneId);
			foreach (var enemy in enemiesOutsideOfZone)
			{
				enemy.PlaceSpawner();
			}
			var spawnersInsideOfZone = GetTree().GetNodesInGroup("EnemySpawner").OfType<EnemySpawner>().Where(q => q.ZoneId == zoneId);
			foreach (var spawner in spawnersInsideOfZone)
			{
				spawner.Target = target;
				spawner.SpawnEnemy(parent);
			}
		}
	}
}
