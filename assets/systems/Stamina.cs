using Godot;

public partial class Stamina : Node
{
    [Signal] public delegate void StaminaDamageTakenEventHandler(float amount);
    [Signal] public delegate void StaggeredEventHandler();
    [Signal] public delegate void UnstaggeredEventHandler();
    [Export] public float MaxStamina = 50f;
    [Export] public float RecoveryTime = 5f;
    [Export] public float StaggerDuration = 5f;
    [Export] public float CurrentStamina = 50f;
    public bool IsStaggered = false;
    public bool CanRecoverStagger = true;
    private float _startRecoveryTimer = 0f;
    private float _staggerTimer = 0f;
    public void TakeStaminaDamage(float amount)
    {
        if (IsStaggered)
        {
            return;
        }
        var actualDamage = Mathf.Max(0, amount);
        CurrentStamina -= actualDamage;
        _startRecoveryTimer = 0f;
        EmitSignal(SignalName.StaminaDamageTaken, amount);
        if (CurrentStamina <= 0)
        {
            HandleStagger();
        }
    }
    public void HandleStagger()
    {
        if (IsStaggered)
        {
            return;
        }
        IsStaggered = true;
        EmitSignal(SignalName.Staggered);
    }
    public void HandleUnstaggeredRecovery(float delta)
    {
        if (_startRecoveryTimer < RecoveryTime)
        {
            _startRecoveryTimer = Mathf.Min(RecoveryTime, _startRecoveryTimer + delta);
        }
        else
        {
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + delta);
        }
    }
    public void HandleStaggeredRecovery(float delta)
    {
        if (_staggerTimer < StaggerDuration)
        {
            if (CanRecoverStagger)
            {
                _staggerTimer = Mathf.Min(StaggerDuration, _staggerTimer + (float)delta);
            }
        }
        else
        {
            IsStaggered = false;
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (!IsStaggered)
        {
            HandleUnstaggeredRecovery((float)delta);
        }
        else
        {
            HandleStaggeredRecovery((float)delta);
        }
    }
}
