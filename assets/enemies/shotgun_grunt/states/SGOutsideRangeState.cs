using Godot;
using System;

public partial class SGOutsideRangeState : OutsideRangeState
{
    protected override void OutsideRangeBehavior(float delta)
    {
        base.OutsideRangeBehavior(delta);
        GD.Print("outside range sg");
    }
}
