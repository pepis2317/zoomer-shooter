using System.Collections.Generic;
using Godot;

public partial class CollisionArea : Node3D
{
	[Signal] public delegate void BodyEnteredEventHandler(CollisionArea body);
	[Signal] public delegate void BodyExitedEventHandler(CollisionArea body);
	[Export] public Vector3 HalfExtents = new Vector3(1f, 1f, 1f);
	[Export] public bool DebugDraw = false;
	[Export] public bool IsSweep = false;
	public HashSet<CollisionArea> Overlapping = new();
	private Vector3 _prevPos;
	private ImmediateMesh _debugMesh;
	private MeshInstance3D _debugInstance;
	public override void _Ready()
	{
		_prevPos = GlobalPosition;
		_debugMesh = new ImmediateMesh();
		_debugInstance = new MeshInstance3D
		{
			Mesh = _debugMesh,
			TopLevel = true,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = Colors.Lime,
			}
		};
		AddChild(_debugInstance);
	}
	public override void _PhysicsProcess(double delta)
	{
		_debugInstance.GlobalTransform = Transform3D.Identity;
		_debugMesh.ClearSurfaces();
		if (!DebugDraw) return;

		DrawBox(GetCollisionBox());
		_prevPos = GlobalPosition;
	}
	private void DrawBox(Aabb box)
	{
		Vector3 p = box.Position;
		Vector3 e = box.End;

		Vector3[] c =
		{
			new(p.X, p.Y, p.Z), new(e.X, p.Y, p.Z), new(e.X, p.Y, e.Z), new(p.X, p.Y, e.Z), // bottom
			new(p.X, e.Y, p.Z), new(e.X, e.Y, p.Z), new(e.X, e.Y, e.Z), new(p.X, e.Y, e.Z), // top
		};

		int[] edges =
		{
			0,1, 1,2, 2,3, 3,0, // bottom
			4,5, 5,6, 6,7, 7,4, // top
			0,4, 1,5, 2,6, 3,7, // verticals
		};

		_debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
		foreach (int i in edges)
			_debugMesh.SurfaceAddVertex(c[i]);
		_debugMesh.SurfaceEnd();
	}

	public Aabb GetCollisionBox()
	{
		if (IsSweep)
		{
			var prev = new Aabb(_prevPos - HalfExtents, HalfExtents * 2f);
			var cur = new Aabb(GlobalPosition - HalfExtents, HalfExtents * 2f);
			return prev.Merge(cur);
		}
		return new Aabb(GlobalPosition - HalfExtents, HalfExtents * 2f);
	}
}
