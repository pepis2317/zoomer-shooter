using Godot;
using System;

public partial class SGInsideRangeState : InsideRangeState
{
	private ShotgunGrunt _shotgunGrunt;
	protected override void OnEnter()
	{
		base.OnEnter();
		_shotgunGrunt = (ShotgunGrunt)_enemy;
	}
	protected override void InsideRangeBehavior(float delta)
	{
		base.InsideRangeBehavior(delta);
	}
}
