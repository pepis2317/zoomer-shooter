using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml.Linq;

public partial class GpuCollisionManager : Node
{
	public static GpuCollisionManager Instance;
	private const int AabbStride = 32; // 2 x vec4
	private const int EntryStride = 12; // codeLo, codeHi, index
	private const int ParamsSize = 16; //std140: float + uint + uint, padded
	private const int LocalSize = 64;
	private const float Cell = 8f;

	private readonly List<CollisionArea> _areas = new();
	private readonly List<CollisionArea> _pendingAdd = new();
	private readonly List<CollisionArea> _pendingRemove = new();

	private RenderingDevice _rd;
	private Rid _mortonShader, _mortonPipeline, _mortonUniformSet;
	private Rid _sortShader, _sortPipeline, _sortUniformSet;
	private Rid _aabbBuffer, _sortEntryBuffer, _mortonParamsBuffer;
	private int _capacity;

	private float[] _aabbFloats = Array.Empty<float>();
	private byte[] _aabbBytes = Array.Empty<byte>();
	private byte[] _entryBytes = Array.Empty<byte>();
	private readonly byte[] _paramBytes = new byte[ParamsSize];

	public void Register(CollisionArea area)
	{
		_pendingAdd.Add(area);
	}

	public void UnRegister(CollisionArea area)
	{
		_pendingRemove.Add(area);
	}

	public void Clear()
	{
		_areas.Clear();
		_pendingAdd.Clear();
		_pendingRemove.Clear();

		// Free the capacity-dependent resources; EnsureCapacity rebuilds them on demand.
		// The uniform set goes first because it references the buffers.
		Free(ref _sortUniformSet);
		Free(ref _mortonUniformSet);
		Free(ref _aabbBuffer);
		Free(ref _sortEntryBuffer);

		_aabbFloats = Array.Empty<float>();
		_aabbBytes = Array.Empty<byte>();
		_entryBytes = Array.Empty<byte>();
		_capacity = 0;   // so EnsureCapacity reallocates next time
	}

	private void Free(ref Rid rid)
	{
		if (rid.IsValid) _rd.FreeRid(rid);
		rid = default;   // so a second call is a no-op
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

		_mortonParamsBuffer = _rd.UniformBufferCreate(ParamsSize);
		EnsureCapacity(2048);
	}

	private void EnsureCapacity(int n)
	{
		if (n <= _capacity) return;

		uint paddedCap = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(2048, n));

		if (_sortUniformSet.IsValid) _rd.FreeRid(_sortUniformSet);
		if (_mortonUniformSet.IsValid) _rd.FreeRid(_mortonUniformSet);
		if (_aabbBuffer.IsValid) _rd.FreeRid(_aabbBuffer);
		if (_sortEntryBuffer.IsValid) _rd.FreeRid(_sortEntryBuffer);

		_aabbBuffer = _rd.StorageBufferCreate(paddedCap * AabbStride);
		_sortEntryBuffer = _rd.StorageBufferCreate(paddedCap * EntryStride);

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
	}

	private static RDUniform MakeUniform(RenderingDevice.UniformType type, int binding, Rid buffer)
	{
		var u = new RDUniform { UniformType = type, Binding = binding };
		u.AddId(buffer);
		return u;
	}

	public void FlushPending()
	{
		if (_pendingAdd.Count > 0)
		{
			_areas.AddRange(_pendingAdd);
			_pendingAdd.Clear();
		}
		if (_pendingRemove.Count > 0)
		{
			_areas.RemoveAll(a => _pendingRemove.Contains(a));
			_pendingRemove.Clear();
		}
	}


	public override void _PhysicsProcess(double delta)
	{
		FlushPending();
		int n = _areas.Count;
		if (n == 0) return;

		uint paddedN = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)n);

		EnsureCapacity(n);
		ulong t0 = Time.GetTicksUsec();
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
		}
		int aabbByteCount = n * AabbStride;
		Buffer.BlockCopy(_aabbFloats, 0, _aabbBytes, 0, aabbByteCount);
		_rd.BufferUpdate(_aabbBuffer, 0, (uint)aabbByteCount, _aabbBytes);

		// Params
		Buffer.BlockCopy(new[] { Cell }, 0, _paramBytes, 0, 4);
		Buffer.BlockCopy(new[] { paddedN }, 0, _paramBytes, 4, 4);
		Buffer.BlockCopy(new[] { (uint)n }, 0, _paramBytes, 8, 4);
		_rd.BufferUpdate(_mortonParamsBuffer, 0, ParamsSize, _paramBytes);

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
		byte[] pc = new byte[16];
		BitConverter.GetBytes(paddedN).CopyTo(pc, 8);

		for (uint blockSize = 2; blockSize <= paddedN; blockSize <<= 1)
		{
			for (uint stride = blockSize >> 1; stride > 0; stride >>= 1)
			{
				BitConverter.GetBytes(blockSize).CopyTo(pc, 0);
				BitConverter.GetBytes(stride).CopyTo(pc, 4);
				_rd.ComputeListSetPushConstant(list, pc, (uint)pc.Length);
				_rd.ComputeListDispatch(list, groups, 1, 1);
				_rd.ComputeListAddBarrier(list);
			}
		}

		_rd.ComputeListEnd();
		_rd.Submit();
		_rd.Sync(); // this might bite me in the ass;
		byte[] data = _rd.BufferGetData(_sortEntryBuffer, 0, (uint)(n * EntryStride));
		ReadEntries(data, n);

		ulong t1 = Time.GetTicksUsec();
		// GD.Print($"gpu path: {(t1 - t0) / 1000.0:F3} ms for n={n}");
	}

	private void ReadEntries(byte[] data, int n)
	{
		var u = MemoryMarshal.Cast<byte, uint>(data);
		for (int i = 1; i < n; i++)
		{
			ulong prev = ((ulong)u[(i - 1) * 3 + 1] << 32) | u[(i - 1) * 3];
			ulong cur = ((ulong)u[i * 3 + 1] << 32) | u[i * 3];
			if (prev > cur) { GD.PrintErr($"Not sorted at {i}"); break; }
		}
	}

	public override void _ExitTree()
	{
		if (_rd == null) return;
		Free(ref _sortUniformSet);
		Free(ref _mortonUniformSet);
		Free(ref _aabbBuffer);
		Free(ref _sortEntryBuffer);
		Free(ref _mortonParamsBuffer);
		Free(ref _mortonPipeline);
		Free(ref _mortonShader);
		Free(ref _sortEntryBuffer);
		Free(ref _sortPipeline);
		_rd.Free();
		_rd = null;
	}
}
