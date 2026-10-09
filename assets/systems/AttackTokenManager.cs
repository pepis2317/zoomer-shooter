using Godot;
using System;
using System.ComponentModel.DataAnnotations;

public partial class AttackTokenManager : Node
{
	private static int _available = 100;
	private static int _max;
	public static AttackTokenManager Instance;
	public override void _Ready()
	{
		Instance = this;
		_max = _available;
	}
	public void Reset()
	{
		_available = _max;
	}
	public bool RequestAttack(int cost)
	{
		if (_available - cost > 0)
		{
			_available -= cost;
			return true;
		}
		return false;
	}
	public void ReturnToken(int amount)
	{
		_available += amount;
	}
}
