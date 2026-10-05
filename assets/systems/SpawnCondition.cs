using Godot;
using System;

[GlobalClass]
public abstract partial class SpawnCondition : Resource
{
    [Signal] public delegate void SatisfiedEventHandler(SpawnCondition condition);
    public abstract void Start();
    public abstract void Stop();
}
