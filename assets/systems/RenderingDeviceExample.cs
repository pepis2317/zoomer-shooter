using Godot;
using System;

public partial class RenderingDeviceExample : Node
{
	private RenderingDevice _rd;
	private Rid _buffer;
	private int _framesWaited = -1;
	public override void _Ready()
	{
		_rd = RenderingServer.CreateLocalRenderingDevice();

		var shaderFile = GD.Load<RDShaderFile>("res://assets/systems/compute_example.glsl");
		var shaderBytecode = shaderFile.GetSpirV();
		var shader = _rd.ShaderCreateFromSpirV(shaderBytecode);

		// We weant to pass an input array to our shader, multiply each element by 2 and get the results
		float[] input = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
		var inputBytes = new Byte[input.Length * sizeof(float)];
		Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);
		// Create a storage buffer that can hold out float values
		// Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
		_buffer = _rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);

		// Create a uniform to assign the buffer to the rendering device
		var uniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 0
		};
		uniform.AddId(_buffer);
		var uniformSet = _rd.UniformSetCreate([uniform], shader, 0);
		// Steps we need to do to compute our result:
		// 1. Create a new pipeline
		// 2. Begin a list of instructions for out GPU to execute
		// 3. Bind out compute list to our pipeline
		// 4. Bind our buffer uniform to our pipeline
		// 5. Specify how many workgroups to use
		// 6. End the list of instructions
		var pipeline = _rd.ComputePipelineCreate(shader);
		var computeList = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(computeList, pipeline);
		_rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
		// We are dispatching the compute shaders with 5 work groups in x axis, and 1 on the other axes
		// Since we have 2 local invocations in the x axis, 10 compute shader invocations will be launched in total
		// Do not read/write to indices outside of the range of the buffer.
		// You have 5 teams of 2 workers:
		// Work group 0:
		// - worker 0
		// - worker 1
		// Work group 1:
		// - worker 2
		// - worker 3
		// etc.
		_rd.ComputeListDispatch(computeList, xGroups: 5, yGroups: 1, zGroups: 1);
		_rd.ComputeListEnd();
		_rd.Submit();
		_framesWaited = 0;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (_framesWaited < 0) return;

		_framesWaited++;
		if (_framesWaited < 3) return; // wait 3 frames

		_rd.Sync();

		var outputBytes = _rd.BufferGetData(_buffer);
		var output = new float[10];
		Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);
		GD.Print("Output: ", string.Join(", ", output));

		_framesWaited = -1; // done
	}
}
