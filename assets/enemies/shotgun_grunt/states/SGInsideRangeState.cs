using System.Runtime.Serialization;
using Godot;

public partial class SGInsideRangeState : InsideRangeState
{
	private ShotgunGrunt _shotgunGrunt;
	private float _repositionTimer = 0f;
	protected override void OnEnter()
	{
		base.OnEnter();
		_shotgunGrunt = (ShotgunGrunt)_enemy;
	}

	private Vector3 GetRandomQuadrantDirection(Vector3 forward)
	{
		forward.Y = 0f;
		forward = forward.Normalized();
		var side = GD.Randf() < 0.5 ? 1 : -1;
		var angleDeg = GD.RandRange(45, 90) * side;
		return forward.Rotated(Vector3.Up, Mathf.DegToRad(angleDeg));
	}

	private Vector3 FindRepositionPoint()
	{
		var mapRid = _shotgunGrunt.NavigationAgent.GetNavigationMap();
		var attempts = 8;
		for (int i = 0; i < attempts; i++)
		{
			var dir = GetRandomQuadrantDirection(_shotgunGrunt.Basis * Vector3.Forward);
			var candidate = _shotgunGrunt.GlobalPosition + dir * _shotgunGrunt.RepositionBoost;
			var snapped = NavigationServer3D.MapGetClosestPoint(mapRid, candidate);
			if (snapped.DistanceTo(candidate) > 2)
			{
				continue;
			}
			var path = NavigationServer3D.MapGetPath(mapRid, _shotgunGrunt.GlobalPosition, snapped, true);
			if (path.Length > 0 && path[path.Length - 1].DistanceTo(snapped) < 1f)
			{
				return snapped;
			}
		}
		return _shotgunGrunt.GlobalPosition;
	}

	private bool CanFire()
	{
		var space = _shotgunGrunt.GetWorld3D().DirectSpaceState;
		Vector3 pos = _shotgunGrunt.GlobalPosition;
		_shotgunGrunt.RangeQuery.From = pos;
		_shotgunGrunt.RangeQuery.To = pos + _shotgunGrunt.Basis * Vector3.Forward * _shotgunGrunt.FiringDistance;

		var result = space.IntersectRay(_shotgunGrunt.RangeQuery);
		if (result.Count == 0) return false;
		if (_shotgunGrunt.FireTimer <= 0)
		{
			if (GD.Randf() < 0.5f)
			{
				return AttackTokenManager.Instance.RequestAttack(_shotgunGrunt.AttackCost);
			}
			return false;
		}
		return false;
	}

	protected override void InsideRangeBehavior(float delta)
	{
		base.InsideRangeBehavior(delta);
		if (CanFire())
		{
			_shotgunGrunt.FireWeapon();
		}
		if (!_shotgunGrunt.IsRepositioning)
		{
			_repositionTimer += delta;

			if (_repositionTimer >= _shotgunGrunt.RepositionCooldown)
			{
				_repositionTimer = 0f;

				if (GD.Randf() < 0.5f)
				{
					_shotgunGrunt.InitReposition(FindRepositionPoint());
				}
			}
		}
		else
		{
			_shotgunGrunt.HandleFollowNavigation(delta);
		}
	}

	protected override void OnExit()
	{
		base.OnExit();
		_repositionTimer = 0f;
	}
}
