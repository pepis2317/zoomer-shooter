using Godot;
using System;

public partial class AttackTokenManager : Node
{
	private static int _available = 100;
	public static AttackTokenManager Instance;
	public override void _Ready()
	{
		Instance = this;
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
