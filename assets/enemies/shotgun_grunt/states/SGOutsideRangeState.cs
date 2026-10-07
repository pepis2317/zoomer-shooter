using Godot;
using System;

public partial class SGOutsideRangeState : OutsideRangeState
{
    protected override void OnEnter()
    {
        base.OnEnter();
        EnemyNavigationManager.Instance.Register(_enemy);
    }
    protected override void OutsideRangeBehavior(float delta)
    {
        base.OutsideRangeBehavior(delta);
        _enemy.HandleFollowNavigation(delta);
    }
    protected override void OnExit()
    {
        base.OnExit();
        EnemyNavigationManager.Instance.UnRegister(_enemy);
    }
}
