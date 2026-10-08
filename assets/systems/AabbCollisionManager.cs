using System;
using System.Collections.Generic;
using Godot;

public partial class AabbCollisionManager : Node
{
	public static AabbCollisionManager Instance;
	private List<CollisionArea> _areas = new();
	private readonly List<CollisionArea> _pendingRemoval = new();
	private struct BvhNode
	{
		public Aabb Bounds;
		public int Left, Right; // child node indices, -1 for leaves
		public int CollisionAreaIndex; // object index for leaves, -1 for normal nodes (the index for the registered collider area)
	}

	private struct SortEntry : IComparable<SortEntry>
	{
		public ulong Code; // this is the morton code
		public int CollisionAreaIndex; // the index for the registered collider area
		public int CompareTo(SortEntry other)
		{
			return Code.CompareTo(other.Code);
		}
	}

	private Aabb[] _aabbs; // per collision area, indexed by original collision area index
	private SortEntry[] _entries; // key value pair of collision area index and its morton code

	private BvhNode[] _nodes;
	private int _nodeCount;

	private readonly List<int> _results = new(); // holds indexes of registered colider areas

	private const float _cell = 8f;
	// we want to do this to produce a positive number
	// morton codes store 3 axis in 1 number, ulong is 64 bits, so 64/3 = 21
	// we choose 2^20 because its half of the (2^21 - 1) range
	// it splits the field evenly between negative and positive cells
	// cell -1,048,576 + 1,048,576 = 0 (bottom of the field)
	// cell 0 + 1,048,576 = 1,048,576 (middle)
	// cell +1,048,575 + 1,048,576 = 2,097,151 (top of the field)
	private const int _bias = 1 << 20;

	public override void _Ready()
	{
		Instance = this;
	}

	public void Register(CollisionArea area)
	{
		_areas.Add(area);
		EnsureCapacity(_areas.Count);
	}

	public void UnRegister(CollisionArea area)
	{
		_pendingRemoval.Add(area);
	}

	private void EnsureCapacity(int n)
	{
		if (_aabbs != null && _aabbs.Length >= n)
		{
			return;
		}
		int cap = Mathf.Max(16, n * 2);
		_aabbs = new Aabb[cap];
		_entries = new SortEntry[cap];
		_nodes = new BvhNode[cap * 2 - 1];
	}

	// used to take each position of Vector3I and converts it into a 32 bit uint
	private static uint ToUnsigned(int v) => (uint)(v + _bias);

	private static ulong Part1By2(ulong x)
	{
		x &= 0x1fffff;
		x = (x | (x << 32)) & 0x1f00000000ffff;
		x = (x | (x << 16)) & 0x1f0000ff0000ff;
		x = (x | (x << 8)) & 0x100f00f00f00f00f;
		x = (x | (x << 4)) & 0x10c30c30c30c30c3;
		x = (x | (x << 2)) & 0x1249249249249249;
		return x;
	}

	//this weaves x, y, and z to a singe number using | operator
	private ulong MortonEncode(Vector3I cell)
	{
		ulong x = Part1By2(ToUnsigned(cell.X));
		ulong y = Part1By2(ToUnsigned(cell.Y));
		ulong z = Part1By2(ToUnsigned(cell.Z));
		return x | (y << 1) | (z << 2);
	}

	private Vector3I FloorPosition(Vector3 position)
	{
		int x = Mathf.FloorToInt(position.X / _cell);
		int y = Mathf.FloorToInt(position.Y / _cell);
		int z = Mathf.FloorToInt(position.Z / _cell);
		return new Vector3I(x, y, z);
	}

	private void RebuildBvh(int n)
	{
		for (int i = 0; i < n; i++)
		{
			var aabbCentroid = _aabbs[i].GetCenter();
			_entries[i] = new SortEntry
			{
				Code = MortonEncode(FloorPosition(aabbCentroid)),
				CollisionAreaIndex = i
			};
		}
		Array.Sort(_entries, 0, n);
		_nodeCount = 0;
		Build(0, n - 1);
	}

	private int Build(int first, int last)
	{
		int index = _nodeCount++;
		if (first == last)
		{
			int collisionAreaIndex = _entries[first].CollisionAreaIndex;
			_nodes[index] = new BvhNode
			{
				Bounds = _aabbs[collisionAreaIndex],
				Left = -1,
				Right = -1,
				CollisionAreaIndex = collisionAreaIndex
			};
			return index;
		}
		int split = FindSplit(first, last);
		int left = Build(first, split);
		int right = Build(split + 1, last);

		var mergedBounds = _nodes[left].Bounds.Merge(_nodes[right].Bounds);
		_nodes[index] = new BvhNode
		{
			Bounds = mergedBounds,
			Left = left,
			Right = right,
			CollisionAreaIndex = -1
		};
		return index;
	}

	// pray to john carmack
	private int FindSplit(int first, int last)
	{
		ulong firstCode = _entries[first].Code;
		ulong lastCode = _entries[last].Code;

		if (firstCode == lastCode)
		{
			// identical codes (in same cell), no bit to split
			// just halve the range
			return (first + last) >> 1;
		}
		int common = System.Numerics.BitOperations.LeadingZeroCount(firstCode ^ lastCode);
		int split = first;
		int step = last - first;
		do
		{
			step = (step + 1) >> 1;
			int mid = split + step;
			if (mid < last)
			{
				int prefix = System.Numerics.BitOperations.LeadingZeroCount(firstCode ^ _entries[mid].Code);
				if (prefix > common)
				{
					split = mid;
				}
			}
		} while (step > 1);
		return split;
	}

	//pray to ra
	public void Query(Aabb box)
	{
		_results.Clear();
		Span<int> stack = stackalloc int[128];
		int sp = 0;
		stack[sp++] = 0; // root

		while (sp > 0)
		{
			ref BvhNode node = ref _nodes[stack[--sp]];
			if (!node.Bounds.Intersects(box)) continue;

			if (node.CollisionAreaIndex >= 0)
			{
				_results.Add(node.CollisionAreaIndex);
			}
			else
			{
				stack[sp++] = node.Left;
				stack[sp++] = node.Right;
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		foreach (var area in _pendingRemoval)
		{
			_areas.Remove(area);
			foreach (var other in _areas)
			{
				if (other.Overlapping.Remove(area))
				{
					other.EmitSignal(CollisionArea.SignalName.BodyExited, area);
				}
			}
			area.Overlapping.Clear();
		}
		_pendingRemoval.Clear();

		int n = _areas.Count;
		if (n <= 1) return;

		for (int i = 0; i < n; i++)
		{
			_aabbs[i] = _areas[i].GetCollisionBox();
		}
		RebuildBvh(n);
		for (int i = 0; i < n; i++)
		{
			Query(_aabbs[i]);
			var newOverlaps = new HashSet<CollisionArea>();
			var currentlyOverlapping = _areas[i].Overlapping;
			foreach (int j in _results)
			{
				if (j <= i)
				{
					// if j = i, they're the same index pointing at the same thing
					// if j < i, we've checked that before
					continue;
				}
				if (_aabbs[i].Intersects(_aabbs[j]))
				{
					newOverlaps.Add(_areas[j]);
				}
			}
			foreach(var area in newOverlaps)
			{
				if (!currentlyOverlapping.Contains(area))
				{
					_areas[i].EmitSignal(CollisionArea.SignalName.BodyEntered, area);
				}
			}
			foreach(var area in currentlyOverlapping)
			{
				if (!newOverlaps.Contains(area))
				{
					_areas[i].EmitSignal(CollisionArea.SignalName.BodyExited, area);
				}
			}
		}
	}
}
