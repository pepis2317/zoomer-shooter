using Godot;
using System;

[GlobalClass]
public partial class PoolResource : Resource
{
    [Export] public PackedScene Scene;
    [Export] public int InitialSize = 10;
}
