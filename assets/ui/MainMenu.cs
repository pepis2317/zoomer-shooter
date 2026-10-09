using Godot;

public partial class MainMenu : Control
{
    [Signal] public delegate void ChangeLevelRequestedEventHandler(string scenePath);

    public override void _Ready()
    {
		Input.MouseMode = Input.MouseModeEnum.Visible;
        GetNode<Button>("%PlayButton").Pressed += OnPlayPressed;
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;
        GetNode<Button>("%PlayButton").GrabFocus();
    }

    private void OnPlayPressed()
    {
        EmitSignal(SignalName.ChangeLevelRequested, "res://levels/prototype_level.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}