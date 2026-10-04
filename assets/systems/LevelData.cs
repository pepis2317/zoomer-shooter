using Godot;
using System;

[GlobalClass]
public partial class LevelData : Resource
{
    [Export] public Godot.Collections.Array<PoolResource> EnemyPools;
}
