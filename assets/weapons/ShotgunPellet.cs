using Godot;
using System;

public partial class ShotgunPellet : Projectile
{
	protected override void OnBodyEntered(CollisionArea body)
	{
		base.OnBodyEntered(body);
		if(body.GetParent() is Projectile) return;
		ApplyDamage((Node3D) body.GetParent());
	}
}
