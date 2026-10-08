using Godot;
using System;
using System.Collections.Generic;

public abstract class StateMachine
{
	public Node Node;
	private StateMachine _currentSubState;
	private StateMachine _defaultSubState;
	private StateMachine _parent;
	private Dictionary<Type, StateMachine> _subStates = new();
	private Dictionary<int, StateMachine> _transitions = new();

	public void EnterStateMachine()
	{
		OnEnter();
		if (_currentSubState == null && _defaultSubState != null)
		{
			_currentSubState = _defaultSubState;
		}
		_currentSubState?.EnterStateMachine();
	}
	public void UpdateStateMachine(float delta)
	{
		OnUpdate(delta);
		_currentSubState?.UpdateStateMachine(delta);
	}
	public void ExitStateMachine()
	{
		_currentSubState?.ExitStateMachine();
		OnExit();
	}
	protected virtual void OnEnter() { }
	protected virtual void OnUpdate(float delta) { }
	protected virtual void OnExit() { }
	public void LoadSubState(StateMachine subState)
	{
		if (_subStates.Count == 0)
		{
			_defaultSubState = subState;
		}
		subState._parent = this;
		try
		{
			_subStates.Add(subState.GetType(), subState);
		}
		catch (ArgumentException)
		{
			GD.PrintErr($"State {GetType()} already contains a substate of type {subState.GetType()}");
			return;
		}
	}
	public void AddTransition(StateMachine from, StateMachine to, int trigger)
	{
		if (!_subStates.TryGetValue(from.GetType(), out _))
		{
			GD.PrintErr($"State {GetType()} does not have a substate of type {from.GetType()} to transition from.");
			return;
		}
		if (!_subStates.TryGetValue(to.GetType(), out _))
		{
			GD.PrintErr($"State {GetType()} does not have a substate of type {to.GetType()} to transition from.");
			return;
		}
		try
		{
			from._transitions.Add(trigger, to);
		}
		catch (ArgumentException)
		{
			GD.PrintErr($"State {from} already has a transition defined for trigger {trigger}");
			return;
		}
	}
	public void SendTrigger(int trigger)
	{
		var root = this;
		while (root._parent != null)
		{
			root = root._parent;
		}
		while (root != null)
		{
			if (root._transitions.TryGetValue(trigger, out StateMachine toState))
			{
				root._parent?.ChangeSubState(toState);
				return;
			}
			root = root._currentSubState;
		}
		GD.PrintErr($"Trigger {trigger} was not consumed by any transition");
	}
	private void ChangeSubState(StateMachine state)
	{
		_currentSubState?.ExitStateMachine();
		var newState = _subStates[state.GetType()];
		_currentSubState = newState;
		newState.EnterStateMachine();
	}
}
