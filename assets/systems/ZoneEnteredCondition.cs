using Godot;

[GlobalClass]
public partial class ZoneEnteredCondition : SpawnCondition
{
	[Export] public string AreaId;
	public override void Start()
	{
		MapEvents.Instance.AreaEntered += OnZoneEntered;
	}

	public override void Stop()
	{
		MapEvents.Instance.AreaEntered -= OnZoneEntered;
	}

	private void OnZoneEntered(string areaId)
	{
		if (areaId == AreaId)
		{
			EmitSignal(SpawnCondition.SignalName.Satisfied, this);
		}
	}
}
