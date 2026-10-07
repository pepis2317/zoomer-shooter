using Godot;

public partial class StaggeredState : StateMachine
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
		_enemy.Stamina.Unstaggered += OnUnstaggered;
	}

	protected override void OnUpdate(float delta)
	{
		base.OnUpdate(delta);
		if (_enemy == null)
		{
			return;
		}
		_enemy.Stamina.CanRecoverStagger = _enemy.IsOnFloor();
	}
	
	protected override void OnExit()
	{
		base.OnExit();
	}

	private void OnUnstaggered()
	{
		SendTrigger(EnemyStateTriggers.UNSTAGGERED);
	}
}
