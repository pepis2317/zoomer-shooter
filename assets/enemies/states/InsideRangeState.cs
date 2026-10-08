using Godot;

public partial class InsideRangeState : UnstaggeredState
{
    protected override void OnEnter()
    {
        base.OnEnter();
    }

    protected override void OnUpdate(float delta)
    {
        base.OnUpdate(delta);
        if (GodotObject.IsInstanceValid(_enemy.Target))
        {
            var distSquared = (_enemy.Target.GlobalPosition - _enemy.GlobalPosition).LengthSquared();
            var range = _enemy.EffectiveRange * 1.5f;
            if (distSquared > range * range)
            {
                SendTrigger(EnemyStateTriggers.OUTSIDERANGE);
            }
            InsideRangeBehavior(delta);
        }
    }
    protected virtual void InsideRangeBehavior(float delta) { }

    protected override void OnExit()
    {
        base.OnExit();
    }
}