using Godot;
using System;
using System.Collections.Generic;

public partial class DynamicSpawner : EnemySpawner
{
	[ExportCategory("Dynamic Spawner Settings")]
	[Export] public bool RequireAll = false;
	[Export] public bool OneShot = true;
	[Export] public Godot.Collections.Array<SpawnCondition> Conditions = new();
	private Dictionary<SpawnCondition, bool> _met = new();
	public override void _Ready()
	{
		base._Ready();
		foreach (var condition in Conditions)
		{
			condition.Satisfied += OnConditionSatisfied;
			condition.Start();
		}
	}

	private void OnConditionSatisfied(SpawnCondition spawnCondition)
	{
		_met[spawnCondition] = true;
		if (RequireAll && _met.Count < Conditions.Count)
		{
			return;
		}
		SpawnEnemy(GetParent());
		if (OneShot)
		{
			foreach (var condition in Conditions)
			{
				condition.Stop();
			}
		}
	}

	public override void _ExitTree()
	{
		foreach (var condition in Conditions)
		{
			condition.Stop();
		}
	}
}
