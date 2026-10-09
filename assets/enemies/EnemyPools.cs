using Godot;
using System.Collections.Generic;

public partial class EnemyPools : Node
{
	public static EnemyPools Instance;
	private Dictionary<string, PackedScene> _scenes = new();
	private Dictionary<string, Stack<Enemy>> _pools = new();
	private Dictionary<string, Stack<EnemySpawner>> _spawners = new();
	private Dictionary<Enemy, Enemy.DespawnEnemyEventHandler> _handlers = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public void Clear()
	{
		foreach(var (enemy, handler) in _handlers)
		{
			if (IsInstanceValid(enemy))
			{
				enemy.DespawnEnemy -= handler;
			}
		}
		_handlers.Clear();
		foreach (var pool in _pools.Values)
		{
			foreach (var p in pool)
			{
				if (IsInstanceValid(p))
					p.QueueFree();
			}
		}
		_pools.Clear();
		foreach (var pool in _spawners.Values)
		{
			foreach (var p in pool)
			{
				if (IsInstanceValid(p))
					p.QueueFree();
			}
		}
		_spawners.Clear();
		_scenes.Clear();
	}

	public void LoadEnemies(Godot.Collections.Array<PoolResource> entries, Node3D target)
	{
		foreach (var entry in entries)
		{
			var stack = new Stack<Enemy>();
			string id = "";
			for (int i = 0; i < entry.InitialSize; i++)
			{
				var enemy = entry.Scene.Instantiate<Enemy>();
				id = enemy.Id;
				enemy.Deactivate();
				Enemy.DespawnEnemyEventHandler handler = () => Release(enemy.Id, enemy);
				enemy.Target = target;
				enemy.DespawnEnemy += handler;
				_handlers[enemy] = handler;
				stack.Push(enemy);
			}
			_scenes[id] = entry.Scene;
			_pools[id] = stack;
		}
	}

	public Enemy Acquire(string id, Transform3D transform, float? health, string zoneId)
	{
		if (_pools.TryGetValue(id, out var pool))
		{
			Enemy enemy;
			if (pool.Count == 0)
			{
				var scene = _scenes[id];
				enemy = scene.Instantiate<Enemy>();
				Enemy.DespawnEnemyEventHandler handler = () => Release(id, enemy);
				enemy.DespawnEnemy += handler;
				_handlers[enemy] = handler;
			}
			else
			{
				enemy = pool.Pop();
			}
			enemy.Activate();
			enemy.Transform = transform;
			enemy.Health.CurrentHealth = health == null ? enemy.Health.MaxHealth : (float)health;
			enemy.ZoneId = zoneId;

			return enemy;
		}
		return null;
	}

	public EnemySpawner AcquireSpawner(string id, Transform3D transform, float? health, string zoneId)
	{
		if (_spawners.TryGetValue(id, out var pool))
		{
			var spawner = pool.Pop();
			spawner.Transform = transform;
			spawner.Health = health;
			spawner.ZoneId = zoneId;
			return spawner;
		}
		return null;
	}

	public void Release(string id, Enemy e)
	{
		if (e.GetParent() == null) return;
		e.Deactivate();
		e.GetParent()?.RemoveChild(e);
		_pools[id].Push(e);
	}

	public void ReleaseSpawner(string id, EnemySpawner s)
	{
		if (!_spawners.ContainsKey(id))
		{
			_spawners[id] = new Stack<EnemySpawner>();
		}

		s.GetParent()?.RemoveChild(s);
		_spawners[id].Push(s);
	}

	public override void _ExitTree()
	{
		foreach (var (enemy, handler) in _handlers)
		{
			if (IsInstanceValid(enemy))
			{
				enemy.DespawnEnemy -= handler;
			}
		}
		_handlers.Clear();
		_pools.Clear();
	}

}
