using Godot;
using System;

public partial class Health : Node
{
	[Signal] public delegate void HealthChangedEventHandler(float newHealth, float maxHealth);
	[Signal] public delegate void DamageTakenEventHandler(float amount, Node3D source, Vector3 direction, float knockbackForce, Vector3 position);
	[Signal] public delegate void DiedEventHandler();
	[Export] public float MaxHealth = 100;
	[Export] public bool StartAtMax = true;
	[Export] public float CurrentHealth = 100;
	public bool IsAlive = true;
	public void TakeDamage(float amount, float knockbackForce, Node3D source = null, Vector3 direction = default, Vector3 position = default)
	{
		if (!IsAlive)
		{
			return;
		}
		var actualDamage = Mathf.Max(0, amount);
		CurrentHealth -= actualDamage;
		EmitSignal(SignalName.DamageTaken, actualDamage, source, direction, knockbackForce, position);
		EmitSignal(SignalName.HealthChanged);
		if (CurrentHealth <= 0)
		{
			HandleDeath();
		}
	}
	public void Heal(float amount)
	{
		if (!IsAlive)
		{
			return;
		}
		var actualHeal = Mathf.Max(0, amount);
		CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + actualHeal);
		EmitSignal(SignalName.Died);
	}
	public void HandleDeath()
	{
		if (!IsAlive)
		{
			return;
		}
		IsAlive = false;
		EmitSignal(SignalName.Died);
	}
}
