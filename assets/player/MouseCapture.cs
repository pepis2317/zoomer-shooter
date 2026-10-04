using Godot;
using System;

public partial class MouseCapture : Node
{
	[Export] public bool Debug = false;
	[Export] public Input.MouseModeEnum CurrentMouseMode = Input.MouseModeEnum.Captured;
	public static float DefaultSensitivity = 0.5f;
	public static float MouseSensitivity = 0.005f;
	public bool CaptureMouse = false;
	public Vector2 MouseInput = Vector2.Zero;
	public override void _Ready()
	{
		Input.MouseMode = CurrentMouseMode;
	}
	public override void _UnhandledInput(InputEvent @event)
	{
		CaptureMouse = @event is InputEventMouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured;
		if (CaptureMouse)
		{
			InputEventMouseMotion mouseMotion = (InputEventMouseMotion)@event;
			MouseInput.X += -mouseMotion.Relative.X * MouseSensitivity;
			MouseInput.Y += -mouseMotion.Relative.Y * MouseSensitivity;
		}
		if (Debug)
		{
			GD.Print(MouseInput);
		}
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		MouseInput = Vector2.Zero;
	}
}
