using Godot;
using System;
using System.Linq;

public partial class PrototypeLevel : Node
{
	[Export] public LevelData LevelData;
	public override void _Ready()
	{
		var playerSpawner = (PlayerSpawner)GetTree().GetFirstNodeInGroup("PlayerSpawner");
		if (playerSpawner == null)
		{
			return;
		}
		playerSpawner.SpawnPlayer(this);
		EnemyPools.Instance.LoadEnemies(LevelData.EnemyPools);
		// ZoneManager.Instance.InitializeZones();
		// var spawners = GetTree().GetNodesInGroup("EnemySpawner").OfType<EnemySpawner>();
		// foreach (var spawner in spawners)
		// {
		// 	spawner.Target = player;
		// 	spawner.SpawnEnemy(this);
		// }
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
