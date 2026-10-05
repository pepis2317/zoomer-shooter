using Godot;
using System;

public partial class MapEvents : Node
{
	public static MapEvents Instance;
	[Signal] public delegate void EnemiesKilledEventHandler(Enemy e, string zoneId);
	[Signal] public delegate void ItemObtainedEventHandler(string itemId);
	[Signal] public delegate void AreaEnteredEventHandler(string areaId);

	public override void _Ready()
	{
		Instance = this;
	}
}
