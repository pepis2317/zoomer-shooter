using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class GpuCollisionManager : Node
{
	public static GpuCollisionManager Instance;
	private const int AabbStride = 32; // 2 x vec4
	private const int EntryStride = 12; // codeLo, codeHi, index
	private const int ParamsSize = 16; //std140: float + uint, padded
	private const int LocalSize = 64;
	private const float Cell = 8f;

	private readonly List<CollisionArea> _areas = new();
	private readonly List<CollisionArea> _pendingAdd = new();
	private readonly List<CollisionArea> _pendingRemove = new();

	private RenderingDevice _rd;
	private Rid _shader, _pipeline, _aabbBuffer, _entryBuffer, _paramsBuffer, _uniformSet;
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
		Free(ref _uniformSet);
		Free(ref _aabbBuffer);
		Free(ref _entryBuffer);

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

	public override void _Ready()
	{
		Instance = this;
		ProcessPhysicsPriority = 100;
		_rd = RenderingServer.CreateLocalRenderingDevice();

		var shaderFile = GD.Load<RDShaderFile>("res://assets/systems/get_entries_to_sort.glsl");
		var spirv = shaderFile.GetSpirV();
		if (!string.IsNullOrEmpty(spirv.CompileErrorCompute))
		{
			GD.PushError(spirv.CompileErrorCompute);
			return;
		}
		_shader = _rd.ShaderCreateFromSpirV(spirv);
		_pipeline = _rd.ComputePipelineCreate(_shader);
		_paramsBuffer = _rd.UniformBufferCreate(ParamsSize);
		EnsureCapacity(2048);
	}

	private void EnsureCapacity(int n)
	{
		if (n <= _capacity) return;

		int newCap = Math.Max(2048, _capacity);
		while (newCap < n) newCap *= 2;

		if (_uniformSet.IsValid) _rd.FreeRid(_uniformSet);
		if (_aabbBuffer.IsValid) _rd.FreeRid(_aabbBuffer);
		if (_entryBuffer.IsValid) _rd.FreeRid(_entryBuffer);

		_aabbBuffer = _rd.StorageBufferCreate((uint)(newCap * AabbStride));
		_entryBuffer = _rd.StorageBufferCreate((uint)(newCap * EntryStride));

		_aabbFloats = new float[newCap * 8];
		_aabbBytes = new byte[newCap * AabbStride];
		_capacity = newCap;

		// The uniform set references the buffers, so it must be rebuilt whenever they change
		_uniformSet = _rd.UniformSetCreate(new Godot.Collections.Array<RDUniform>
		{
			MakeUniform(RenderingDevice.UniformType.StorageBuffer, 0, _aabbBuffer),
			MakeUniform(RenderingDevice.UniformType.StorageBuffer, 1, _entryBuffer),
			MakeUniform(RenderingDevice.UniformType.UniformBuffer, 2, _paramsBuffer),
		}, _shader, 0);
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
		Buffer.BlockCopy(new[] { (uint)n }, 0, _paramBytes, 4, 4);
		_rd.BufferUpdate(_paramsBuffer, 0, ParamsSize, _paramBytes);

		// Dispatch
		long list = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(list, _pipeline);
		_rd.ComputeListBindUniformSet(list, _uniformSet, 0);
		_rd.ComputeListDispatch(list, (uint)((n + LocalSize - 1) / LocalSize), 1, 1);
		_rd.ComputeListEnd();
		_rd.Submit();
		_rd.Sync(); // this might bite me in the ass;

		byte[] data = _rd.BufferGetData(_entryBuffer, 0, (uint)(n * EntryStride));
		ReadEntries(data, n);
		ulong t1 = Time.GetTicksUsec();
		GD.Print($"gpu path: {(t1 - t0) / 1000.0:F3} ms for n={n}");
	}

	private void ReadEntries(byte[] data, int n)
	{
		var u = MemoryMarshal.Cast<byte, uint>(data);
		for (int i = 0; i < n; i++)
		{
			ulong code = ((ulong)u[i * 3 + 1] << 32) | u[i * 3];
			uint index = u[i * 3 + 2];

			string dec = code.ToString();                                // decimal
			string hex = code.ToString("X16");                           // 16 hex digits
			string bin = Convert.ToString((long)code, 2).PadLeft(63, '0'); // 63-bit binary

			// GD.Print($"[{i}] index={index} dec={dec} hex=0x{hex} bin={bin}");
		}
	}

	public override void _ExitTree()
	{
		if (_rd == null) return;
		Free(ref _uniformSet);
		Free(ref _aabbBuffer);
		Free(ref _entryBuffer);
		Free(ref _paramsBuffer);
		Free(ref _pipeline);
		Free(ref _shader);
		_rd.Free();
		_rd = null;
	}
}
