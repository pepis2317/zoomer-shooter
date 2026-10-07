using Godot;
using System;

public partial class UnstaggeredState : StateMachine
{
	protected Enemy _enemy;
	protected override void OnEnter()
	{
		base.OnEnter();
		if (Node == null)
		{
			GD.Print("Node not initialized");
			return;
		}
		_enemy = (Enemy)Node;
		_enemy.Stamina.Staggered += OnStaggered;
	}

	protected override void OnUpdate(float delta)
	{
		base.OnUpdate(delta);
		if (_enemy == null)
		{
			return;
		}
	}

	protected override void OnExit()
	{
		base.OnExit();
	}

	private void OnStaggered()
	{
		SendTrigger(EnemyStateTriggers.STAGGERED);
	}
}
