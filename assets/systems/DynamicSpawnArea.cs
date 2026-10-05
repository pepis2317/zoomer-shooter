using Godot;
using System;

public partial class DynamicSpawnArea : Area3D
{
	[Export] public string AreaId;
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController)
		{
			MapEvents.Instance.EmitSignal(MapEvents.SignalName.AreaEntered, AreaId);
		}
	}
}
