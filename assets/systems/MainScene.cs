using Godot;
using System;

public partial class MainScene : Node
{
	private Node _currentLevel;
	private PauseMenu _pauseMenu;
	public override void _Ready()
	{
		_currentLevel = GetNode("%CurrentLevel");
		_pauseMenu = GetNode<PauseMenu>("%PauseMenu");
		_pauseMenu.ResumeRequested += Resume;
		_pauseMenu.MainMenuRequested += () =>
		{
			Resume();
			ChangeLevel("res://assets/ui/main_menu.tscn");
		};
		ChangeLevel("res://assets/ui/main_menu.tscn");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("pause")) return;
		if (!CurrentLevelIsPausable()) return;
		if (GetTree().Paused) Resume();
		else Pause();

		GetViewport().SetInputAsHandled();
	}

	private bool CurrentLevelIsPausable()
	{
		foreach (var child in _currentLevel.GetChildren())
		{
			if (child is IPausable) return true;
		}
		return false;
	}

	private void Resume()
	{
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_pauseMenu.Close();
	}

	private void Pause()
	{
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_pauseMenu.Open();
	}

	public void ChangeLevel(string scenePath)
	{
		CallDeferred(MethodName.ChangeLevelInternal, scenePath);
	}

	private void ChangeLevelInternal(string scenePath)
	{
		// 1. Remove old level right now so nothing in it processes anymore
		foreach (var child in _currentLevel.GetChildren())
		{
			_currentLevel.RemoveChild(child);
			child.QueueFree();
		}

		// 2. Now nothing can call into the managers, so clear them
		GpuCollisionManager.Instance.Clear();
		EnemyNavigationManager.Instance.Clear();
		EnemyPools.Instance.Clear();
		ProjectilePools.Instance.Clear();
		AttackTokenManager.Instance.Reset();
		AabbCollisionManager.Instance.Clear();

		// 3. Load the new level; its _Ready can register freely
		var level = GD.Load<PackedScene>(scenePath).Instantiate();
		_currentLevel.AddChild(level);

		if (level.HasSignal("ChangeLevelRequested"))
		{
			level.Connect("ChangeLevelRequested", Callable.From<string>(ChangeLevel));
		}
	}
}
