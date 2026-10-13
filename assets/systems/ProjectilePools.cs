using Godot;
using System.Collections.Generic;

public partial class ProjectilePools : Node
{
	public static ProjectilePools Instance;
	private Dictionary<PackedScene, Stack<Projectile>> _pools = new();
	private Dictionary<Projectile, Projectile.DespawnProjectileEventHandler> _handlers = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public void Clear()
	{
		foreach (var (projectile, handler) in _handlers)
		{
			if (IsInstanceValid(projectile))
			{
				projectile.DespawnProjectile -= handler;
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
	}

	public void RegisterProjectiles(PackedScene scene, int amount)
	{
		var stack = new Stack<Projectile>();
		for (int i = 0; i < amount; i++)
		{
			var projectile = scene.Instantiate<Projectile>();
			projectile.Deactivate();
			Projectile.DespawnProjectileEventHandler handler = () => Release(scene, projectile);
			projectile.DespawnProjectile += handler;
			_handlers[projectile] = handler;
			stack.Push(projectile);
		}
		_pools[scene] = stack;
	}

	public Projectile Acquire(PackedScene scene)
	{
		if (_pools.TryGetValue(scene, out var pool))
		{
			Projectile p;
			if (pool.Count == 0)
			{
				p = scene.Instantiate<Projectile>();
				Projectile.DespawnProjectileEventHandler handler = () => Release(scene, p);
				p.DespawnProjectile += handler;
				_handlers[p] = handler;
			}
			else
			{
				p = pool.Pop();
			}
			return p;
		}
		return null;
	}

	public void Release(PackedScene scene, Projectile p)
	{
		if (p.GetParent() == null) return;
		p.Deactivate();
		p.GetParent().RemoveChild(p);
		_pools[scene].Push(p);
	}

	public override void _ExitTree()
	{
		foreach (var (projectile, handler) in _handlers)
		{
			if (IsInstanceValid(projectile))
			{
				projectile.DespawnProjectile -= handler;
			}
		}
		_handlers.Clear();
		_pools.Clear();
	}
}
