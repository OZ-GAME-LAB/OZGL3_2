public class PlayerStatViewModel : UnitVM
{
    public override bool CanReceiveBossPoison => true;

    public override void InvokeOnceOnInit()
    {
        base.InvokeOnceOnInit();
        OnPropertyChanged(nameof(MoveSpeed));
        OnPropertyChanged(nameof(JumpForce));
        OnPropertyChanged(nameof(AttackPower));
    }

    private float _moveSpeed;
    public float MoveSpeed
    {
        get => _moveSpeed;
        set
        {
            if (_moveSpeed != value)
            {
                _moveSpeed = value;
                OnPropertyChanged(nameof(MoveSpeed));
            }
        }
    }

    private float _jumpForce;
    public float JumpForce
    {
        get => _jumpForce;
        set
        {
            if (_jumpForce != value)
            {
                _jumpForce = value;
                OnPropertyChanged(nameof(JumpForce));
            }
        }
    }

    private float _attackPower;
    public float AttackPower
    {
        get => _attackPower;
        set
        {
            if (_attackPower == value)
                return;

            _attackPower = value;
            OnPropertyChanged(nameof(AttackPower));
        }
    }
}
