using Godot;
using System;

public partial class CameraController : Node3D
{
	[ExportCategory("References")]
	[Export] public PlayerController PlayerController;
	[Export] public MouseCapture MouseCaptureComponent;
	[ExportCategory("References")]
	[Export(PropertyHint.Range, "-90, -60")] public int TiltLowerLimit = -90;
	[Export(PropertyHint.Range, "60, -90")] public int TiltUpperLimit = 90;
	public Vector3 CameraRotation = Vector3.Zero;
	public void UpdateCameraRotation(Vector2 input)
	{
		CameraRotation.X += input.Y;
		CameraRotation.Y += input.X;
		CameraRotation.X = Mathf.Clamp(CameraRotation.X, Mathf.DegToRad(TiltLowerLimit), Mathf.DegToRad(TiltUpperLimit));

		var playerRotation = new Vector3(0f, CameraRotation.Y, 0f);
		var cameraRotation = new Vector3(CameraRotation.X, 0f, CameraRotation.Z);

		Transform3D transform = Transform;
		transform.Basis = Basis.FromEuler(cameraRotation);
		Transform = transform;
		CameraRotation.Z = 0;

		PlayerController.UpdateRotation(playerRotation);
	}
	public void SetLook(float pitch, float yaw)
	{
		CameraRotation = new Vector3(pitch, yaw, 0f);
		UpdateCameraRotation(Vector2.Zero);
	}
	public override void _Process(double delta)
	{
		UpdateCameraRotation(MouseCaptureComponent.MouseInput);
	}
}
