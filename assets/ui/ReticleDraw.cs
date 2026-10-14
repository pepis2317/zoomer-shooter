using Godot;
using System;

public partial class ReticleDraw : Control
{
	[Export] public float Radius = 30;
	[Export] public float Thickness = 1;
	[Export] public float Gap = 5f;
	[Export] public Color Color = Colors.White;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Draw()
	{
		Vector2 center = Size / 2;

		// Top line
		DrawLine(
			center + new Vector2(0, -Gap),
			center + new Vector2(0, -Gap - Radius),
			Color, Thickness
		);

		// Bottom line
		DrawLine(
			center + new Vector2(0, Gap),
			center + new Vector2(0, Gap + Radius),
			Color, Thickness
		);

		// Left line
		DrawLine(
			center + new Vector2(-Gap, 0),
			center + new Vector2(-Gap - Radius, 0),
			Color, Thickness
		);

		// Right line
		DrawLine(
			center + new Vector2(Gap, 0),
			center + new Vector2(Gap + Radius, 0),
			Color, Thickness
		);
	}
}
