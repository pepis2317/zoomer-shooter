using Godot;
using System;
using System.Linq;

public partial class PrototypeLevel : Node, IPausable
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
		var player = (PlayerController)GetTree().GetFirstNodeInGroup("Player");
		EnemyPools.Instance.LoadEnemies(LevelData.EnemyPools, player);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
