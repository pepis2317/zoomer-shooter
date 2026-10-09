using Godot;
using System;
using System.Collections.Generic;
public interface INavigationTickable
{
	public void NavigationTick();
}
public partial class EnemyNavigationManager : Node
{
	public static EnemyNavigationManager Instance;
	[Export(PropertyHint.Range, "0.05,5,0.05")]
	public float RepathInterval = 0.5f;
	[Export(PropertyHint.Range, "1,64,1")]
	public int MaxTicksPerFrame = 4;
	[Export] public float FarDistance = 20f;
	[Export] public float VeryFarDistance = 40f;

	private readonly List<INavigationTickable> _agents = new();
	private int _cursor;
	private double _budget;

	public override void _Ready()
	{
		Instance = this;
	}

	public void Clear()
	{
		_agents.Clear();
	}

	public void Register(INavigationTickable agent)
	{
		if (agent == null || _agents.Contains(agent))
		{
			return;
		}
		_agents.Add(agent);
	}

	public void UnRegister(INavigationTickable agent)
	{
		var index = _agents.IndexOf(agent);
		if (index < 0)
		{
			return;
		}
		_agents.RemoveAt(index);
		if (index < _cursor)
		{
			_cursor--;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		int count = _agents.Count;
		if (count == 0)
		{
			return;
		}
		_budget += count * delta / Math.Max(RepathInterval, 0.01f);
		int ticks = (int)_budget;
		_budget -= ticks;
		for (int i = 0; i < ticks && _agents.Count > 0; i++)
		{
			if (_cursor >= _agents.Count)
			{
				_cursor = 0;
			}
			INavigationTickable agent = _agents[_cursor];
			if (agent is Enemy enemy && !IsInstanceValid(enemy))
			{
				_agents.RemoveAt(_cursor);
				i--;
				continue;
			}
			_cursor++;
			agent.NavigationTick();
		}
	}
}