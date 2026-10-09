using Godot;

public partial class PauseMenu : CanvasLayer
{
	[Signal] public delegate void ResumeRequestedEventHandler();
	[Signal] public delegate void MainMenuRequestedEventHandler();
	public override void _Ready()
	{
		Visible = false;
		GetNode<Button>("%ResumeButton").Pressed += () => EmitSignal(SignalName.ResumeRequested);
		GetNode<Button>("%MainMenuButton").Pressed += () => EmitSignal(SignalName.MainMenuRequested);
		GetNode<Button>("%QuitButton").Pressed += () => GetTree().Quit();
	}

	public void Open()
	{
		Visible = true;
		GetNode<Button>("%ResumeButton").GrabFocus();
	}

	public void Close() => Visible = false;
}
