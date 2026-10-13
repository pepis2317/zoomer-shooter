using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public partial class GpuCollisionManager : Node
{
	public static GpuCollisionManager Instance;
	private const int AabbStride = 32; // 2 x vec4
	private const int EntryStride = 12; // codeLo, codeHi, index
	private const int NodeStride = 16; // left, right, parent, pad (pad isnt set anywhere here)
	private const int MortonParamsSize = 16; // std140: float + uint + uint, padded
	private const int SortParamsSize = 16; // blockSize + stride + count + pad (pad isnt set anywhere here though, its just padding)
	private const int BuildParamsSize = 16; // count + p0 + p1 + p2
	private const int BoundsParamsSize = 16; // count + p0 + p1 + p2
	private const int TraverseParamsSize = 16; // count + maxpairs + p1 + p2
	private const int LocalSize = 256;
	private const float Cell = 8f;

	private readonly List<CollisionArea> _areas = new();
	private readonly List<CollisionArea> _pendingRemove = new();
	private Dictionary<CollisionArea, HashSet<CollisionArea>> _justOverlapped = new();

	private RenderingDevice _rd;
	private Rid _mortonShader, _mortonPipeline, _mortonUniformSet;
	private Rid _sortShader, _sortPipeline, _sortUniformSet;
	private Rid _buildShader, _buildPipeline, _buildUniformSet;
	private Rid _boundsShader, _boundsPipeline, _boundsUniformSet;
	private Rid _traversalShader, _traversalPipeline, _traversalUniformSet;
	private Rid _aabbBuffer, _sortEntryBuffer, _mortonParamsBuffer, _nodesBuffer, _boundsBuffer, _countersBuffer, _pairCounterBuffer, _pairsBuffer;
	private int _capacity;

	private float[] _aabbFloats = Array.Empty<float>();
	private byte[] _aabbBytes = Array.Empty<byte>();
	private byte[] _entryBytes = Array.Empty<byte>();
	private readonly byte[] _mortonParamBytes = new byte[MortonParamsSize];
	private MeshInstance3D _debugMesh;

	public void Register(CollisionArea area)
	{
		_areas.Add(area);
	}

	public void UnRegister(CollisionArea area)
	{
		_pendingRemove.Add(area);
	}

	public void Clear()
	{
		_areas.Clear();
		_pendingRemove.Clear();

		// Free the capacity-dependent resources; EnsureCapacity rebuilds them on demand.
		// The uniform set goes first because it references the buffers.
		FreeUniformSets();
		FreeBuffers();

		_aabbFloats = Array.Empty<float>();
		_aabbBytes = Array.Empty<byte>();
		_entryBytes = Array.Empty<byte>();
		_capacity = 0;
	}

	private void Free(ref Rid rid)
	{
		if (rid.IsValid) _rd.FreeRid(rid);
		rid = default;
	}

	private bool LoadCompute(string path, out Rid shader, out Rid pipeline)
	{
		shader = default;
		pipeline = default;

		var file = GD.Load<RDShaderFile>(path);
		var spirv = file.GetSpirV();
		if (!string.IsNullOrEmpty(spirv.CompileErrorCompute))
		{
			GD.PushError(spirv.CompileErrorCompute);
			return false;
		}
		shader = _rd.ShaderCreateFromSpirV(spirv);
		pipeline = _rd.ComputePipelineCreate(shader);
		return true;
	}

	public override void _Ready()
	{
		Instance = this;
		ProcessPhysicsPriority = 100;
		_rd = RenderingServer.CreateLocalRenderingDevice();

		if (!LoadCompute("res://assets/systems/get_entries_to_sort.glsl", out _mortonShader, out _mortonPipeline)) return;
		if (!LoadCompute("res://assets/systems/bitonic_step.glsl", out _sortShader, out _sortPipeline)) return;
		if (!LoadCompute("res://assets/systems/build_lbvh_hierarchy.glsl", out _buildShader, out _buildPipeline)) return;
		if (!LoadCompute("res://assets/systems/bottom_up_bounds.glsl", out _boundsShader, out _boundsPipeline)) return;
		if (!LoadCompute("res://assets/systems/traversal.glsl", out _traversalShader, out _traversalPipeline)) return;

		EnsureCapacity(2048);
	}

	private void EnsureCapacity(int n)
	{
		if (n <= _capacity) return;

		uint paddedCap = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(2048, n));

		FreeUniformSets();
		FreeBuffers();

		_aabbBuffer = _rd.StorageBufferCreate(paddedCap * AabbStride);
		_sortEntryBuffer = _rd.StorageBufferCreate(paddedCap * EntryStride);
		_nodesBuffer = _rd.StorageBufferCreate(2 * paddedCap * NodeStride);
		_boundsBuffer = _rd.StorageBufferCreate(2 * paddedCap * AabbStride);
		_mortonParamsBuffer = _rd.UniformBufferCreate(MortonParamsSize);
		_countersBuffer = _rd.StorageBufferCreate(paddedCap * 4); // one uint per internal node
		_pairCounterBuffer = _rd.StorageBufferCreate(4); // just one uint
		_pairsBuffer = _rd.StorageBufferCreate(paddedCap * 8); // each uvec2 is two uints, so 8

		_aabbFloats = new float[paddedCap * 8];
		_aabbBytes = new byte[paddedCap * AabbStride];
		_capacity = (int)paddedCap;

		// The uniform set references the buffers, so it must be rebuilt whenever they change
		_mortonUniformSet = _rd.UniformSetCreate(new Godot.Collections.Array<RDUniform>
		{
			MakeUniform(RenderingDevice.UniformType.StorageBuffer, 0, _aabbBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer, 1, _sortEntryBuffer),
			MakeUniform(RenderingDevice.UniformType.UniformBuffer, 2, _mortonParamsBuffer),
		}, _mortonShader, 0);

		_sortUniformSet = _rd.UniformSetCreate(new Godot.Collections.Array<RDUniform>
		{
			MakeUniform(RenderingDevice.UniformType.StorageBuffer, 0, _sortEntryBuffer)
		}, _sortShader, 0);

		_buildUniformSet = _rd.UniformSetCreate(new Godot.Collections.Array<RDUniform>
		{
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,0, _sortEntryBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,1, _nodesBuffer)
		}, _buildShader, 0);

		_boundsUniformSet = _rd.UniformSetCreate(new Godot.Collections.Array<RDUniform>
		{
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,0,_sortEntryBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,1,_nodesBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,2, _aabbBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,3, _boundsBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,4, _countersBuffer)

		}, _boundsShader, 0);

		_traversalUniformSet = _rd.UniformSetCreate(new Godot.Collections.Array<RDUniform>
		{
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,0,_sortEntryBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,1,_nodesBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,2,_boundsBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,3,_pairCounterBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer,4,_pairsBuffer),
		}, _traversalShader, 0);
	}

	private static RDUniform MakeUniform(RenderingDevice.UniformType type, int binding, Rid buffer)
	{
		var u = new RDUniform { UniformType = type, Binding = binding };
		u.AddId(buffer);
		return u;
	}

	public void FlushRemoval()
	{
		foreach (var area in _pendingRemove)
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
		_pendingRemove.Clear();
	}

	public override void _PhysicsProcess(double delta)
	{
		//clear
		FlushRemoval();
		int n = _areas.Count;
		if (n == 0) return;

		uint paddedN = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)n);

		EnsureCapacity(n);
		for (int i = 0; i < n; i++)
		{
			var box = _areas[i].GetCollisionBox();
			Vector3 min = box.Position;
			Vector3 max = box.Position + box.Size;
			int offset = i * 8;
			_aabbFloats[offset + 0] = min.X;
			_aabbFloats[offset + 1] = min.Y;
			_aabbFloats[offset + 2] = min.Z;
			_aabbFloats[offset + 3] = 0f;

			_aabbFloats[offset + 4] = max.X;
			_aabbFloats[offset + 5] = max.Y;
			_aabbFloats[offset + 6] = max.Z;
			_aabbFloats[offset + 7] = 0f;
			_areas[i].ResetSweep();
		}
		int aabbByteCount = n * AabbStride;
		Buffer.BlockCopy(_aabbFloats, 0, _aabbBytes, 0, aabbByteCount);
		_rd.BufferUpdate(_aabbBuffer, 0, (uint)aabbByteCount, _aabbBytes);

		// Params
		Buffer.BlockCopy(new[] { Cell }, 0, _mortonParamBytes, 0, 4);
		Buffer.BlockCopy(new[] { paddedN }, 0, _mortonParamBytes, 4, 4);
		Buffer.BlockCopy(new[] { (uint)n }, 0, _mortonParamBytes, 8, 4);
		_rd.BufferUpdate(_mortonParamsBuffer, 0, MortonParamsSize, _mortonParamBytes);

		_rd.BufferClear(_pairCounterBuffer, 0, 4);
		if (n >= 2)
		{
			_rd.BufferClear(_countersBuffer, 0, (uint)((n - 1) * 4));
		}

		// Dispatch
		long list = _rd.ComputeListBegin();
		// Pass 1: morton codes
		_rd.ComputeListBindComputePipeline(list, _mortonPipeline);
		_rd.ComputeListBindUniformSet(list, _mortonUniformSet, 0);
		_rd.ComputeListDispatch(list, (paddedN + LocalSize - 1) / LocalSize, 1, 1);
		_rd.ComputeListAddBarrier(list);

		// Pass 2: bitonic sort
		_rd.ComputeListBindComputePipeline(list, _sortPipeline);
		_rd.ComputeListBindUniformSet(list, _sortUniformSet, 0);

		uint groups = (paddedN + LocalSize - 1) / LocalSize;
		byte[] sortingPushConstants = new byte[SortParamsSize];
		BitConverter.GetBytes(paddedN).CopyTo(sortingPushConstants, 8);

		for (uint blockSize = 2; blockSize <= paddedN; blockSize <<= 1)
		{
			for (uint stride = blockSize >> 1; stride > 0; stride >>= 1)
			{
				BitConverter.GetBytes(blockSize).CopyTo(sortingPushConstants, 0);
				BitConverter.GetBytes(stride).CopyTo(sortingPushConstants, 4);
				_rd.ComputeListSetPushConstant(list, sortingPushConstants, (uint)sortingPushConstants.Length);
				_rd.ComputeListDispatch(list, groups, 1, 1);
				_rd.ComputeListAddBarrier(list);
			}
		}
		var maxPairs = paddedN * 8;
		if (n >= 2)
		{
			// Pass 3: build hierarchy
			byte[] buildPushConstants = new byte[BuildParamsSize];
			BitConverter.GetBytes((uint)n).CopyTo(buildPushConstants, 0);
			_rd.ComputeListBindComputePipeline(list, _buildPipeline);
			_rd.ComputeListBindUniformSet(list, _buildUniformSet, 0);
			_rd.ComputeListSetPushConstant(list, buildPushConstants, (uint)buildPushConstants.Length);
			_rd.ComputeListDispatch(list, (uint)((n - 1 + LocalSize - 1) / LocalSize), 1, 1);
			_rd.ComputeListAddBarrier(list);

			// Pass 4: bottom-up bounds
			byte[] boundsPushConstants = new byte[BoundsParamsSize];
			BitConverter.GetBytes((uint)n).CopyTo(boundsPushConstants, 0);
			_rd.ComputeListBindComputePipeline(list, _boundsPipeline);
			_rd.ComputeListBindUniformSet(list, _boundsUniformSet, 0);
			_rd.ComputeListSetPushConstant(list, boundsPushConstants, (uint)boundsPushConstants.Length);
			_rd.ComputeListDispatch(list, (uint)((n + LocalSize - 1) / LocalSize), 1, 1);
			_rd.ComputeListAddBarrier(list);

			// Pass 5: traversal
			byte[] traversalPushConstants = new byte[TraverseParamsSize];
			BitConverter.GetBytes((uint)n).CopyTo(traversalPushConstants, 0);
			_rd.ComputeListBindComputePipeline(list, _traversalPipeline);
			_rd.ComputeListBindUniformSet(list, _traversalUniformSet, 0);
			BitConverter.GetBytes(maxPairs).CopyTo(traversalPushConstants, 4);
			_rd.ComputeListSetPushConstant(list, traversalPushConstants, (uint)traversalPushConstants.Length);
			_rd.ComputeListDispatch(list, (uint)((n + LocalSize - 1) / LocalSize), 1, 1);
			_rd.ComputeListAddBarrier(list);
		}
		_rd.ComputeListEnd();
		_rd.Submit();
		_rd.Sync();
		_justOverlapped.Clear();
		DrawTreeBounds(n, 5);
		if (n >= 2)
		{
			uint count = BitConverter.ToUInt32(_rd.BufferGetData(_pairCounterBuffer, 0, 4), 0);

			// pairCount keeps incrementing past maxPairs, clamp to what was actually written
			if (count > maxPairs) count = maxPairs;

			if (count == 0)
			{
				return;
			}

			byte[] data = _rd.BufferGetData(_pairsBuffer, 0, count * 8);
			var pairs = MemoryMarshal.Cast<byte, uint>(data); // or keep uint
			for (int i = 1; i < pairs.Length; i += 2)
			{
				var a = (int)pairs[i - 1];
				var b = (int)pairs[i];
				HandleOverlap(_areas[a], _areas[b]);
				HandleOverlap(_areas[b], _areas[a]);
			}
			foreach (var (area, justOverlapped) in _justOverlapped)
			{
				var exited = area.Overlapping.Where(q => !justOverlapped.Contains(q)).ToList();

				foreach (var other in exited)
				{
					area.Overlapping.Remove(other);
					area.EmitSignal(CollisionArea.SignalName.BodyExited, other);
				}
			}
		}
	}

	private void HandleOverlap(CollisionArea a, CollisionArea b)
	{
		var overlapping = a.Overlapping;
		if (!overlapping.Contains(b))
		{
			a.Overlapping.Add(b);
			a.EmitSignal(CollisionArea.SignalName.BodyEntered, b);
			if (!_justOverlapped.TryGetValue(a, out var set))
			{
				set = new HashSet<CollisionArea>();
				_justOverlapped[a] = set;
			}
			set.Add(b);
		}
	}

	private void EnsureDebugMesh()
	{
		if (_debugMesh != null) return;
		_debugMesh = new MeshInstance3D
		{
			Mesh = new ImmediateMesh(),
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				VertexColorUseAsAlbedo = true,
			}
		};
		AddChild(_debugMesh);
	}

	public void DrawTreeBounds(int n, int maxDepth = 6)
	{
		EnsureDebugMesh();
		var mesh = (ImmediateMesh)_debugMesh.Mesh;
		mesh.ClearSurfaces();
		if (n < 2) return;

		byte[] b = _rd.BufferGetData(_boundsBuffer);
		byte[] t = _rd.BufferGetData(_nodesBuffer);

		// Node = left, right, parent, pad (16 bytes)
		uint Left(int i) => BitConverter.ToUInt32(t, i * 16 + 0);
		uint Right(int i) => BitConverter.ToUInt32(t, i * 16 + 4);

		Vector3 Min(int i) => new Vector3(
			BitConverter.ToSingle(b, i * 32 + 0),
			BitConverter.ToSingle(b, i * 32 + 4),
			BitConverter.ToSingle(b, i * 32 + 8));
		Vector3 Max(int i) => new Vector3(
			BitConverter.ToSingle(b, i * 32 + 16),
			BitConverter.ToSingle(b, i * 32 + 20),
			BitConverter.ToSingle(b, i * 32 + 24));

		mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

		// Walk from the root, so depth comes for free
		var stack = new Stack<(int node, int depth)>();
		stack.Push((0, 0));
		while (stack.Count > 0)
		{
			var (node, depth) = stack.Pop();
			if (depth > maxDepth) continue;

			Color c = Color.FromHsv((depth * 0.13f) % 1f, 0.9f, 1f);
			AddBox(mesh, Min(node), Max(node), c);

			if (node < n - 1) // internal node
			{
				stack.Push(((int)Left(node), depth + 1));
				stack.Push(((int)Right(node), depth + 1));
			}
		}

		mesh.SurfaceEnd();
	}

	private static void AddBox(ImmediateMesh mesh, Vector3 mn, Vector3 mx, Color c)
	{
		Vector3[] v =
		{
		new(mn.X, mn.Y, mn.Z), new(mx.X, mn.Y, mn.Z),
		new(mx.X, mx.Y, mn.Z), new(mn.X, mx.Y, mn.Z),
		new(mn.X, mn.Y, mx.Z), new(mx.X, mn.Y, mx.Z),
		new(mx.X, mx.Y, mx.Z), new(mn.X, mx.Y, mx.Z),
	};
		int[] e =
		{
		0,1, 1,2, 2,3, 3,0, // back face
        4,5, 5,6, 6,7, 7,4, // front face
        0,4, 1,5, 2,6, 3,7  // connecting edges
    };
		mesh.SurfaceSetColor(c); // applies to the vertices that follow
		foreach (int i in e) mesh.SurfaceAddVertex(v[i]);
	}

	public void FreeUniformSets()
	{
		Free(ref _mortonUniformSet);
		Free(ref _sortUniformSet);
		Free(ref _buildUniformSet);
		Free(ref _boundsUniformSet);
	}

	public void FreeBuffers()
	{
		Free(ref _aabbBuffer);
		Free(ref _sortEntryBuffer);
		Free(ref _mortonParamsBuffer);
		Free(ref _boundsBuffer);
		Free(ref _countersBuffer);
		Free(ref _pairCounterBuffer);
		Free(ref _pairsBuffer);
		Free(ref _nodesBuffer);
	}

	public void FreePipelines()
	{
		Free(ref _mortonPipeline);
		Free(ref _sortPipeline);
		Free(ref _buildPipeline);
		Free(ref _boundsPipeline);
		Free(ref _traversalPipeline);
	}

	public void FreeShaders()
	{
		Free(ref _mortonShader);
		Free(ref _sortShader);
		Free(ref _buildShader);
		Free(ref _boundsShader);
	}

	public override void _ExitTree()
	{
		if (_rd == null) return;

		FreeUniformSets();
		FreeBuffers();
		FreePipelines();
		FreeShaders();
		_rd.Free();
		_rd = null;
	}
}
