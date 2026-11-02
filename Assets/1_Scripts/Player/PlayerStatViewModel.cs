public class PlayerStatViewModel : ViewModelBase
{
    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(Hp));
        OnPropertyChanged(nameof(MaxHp));
        OnPropertyChanged(nameof(MoveSpeed));
        OnPropertyChanged(nameof(JumpForce));
        OnPropertyChanged(nameof(AttackPower));
    }

    private float _hp;
    public float Hp
    {
        get => _hp;
        set
        {
            if (_hp != value)
            {
                _hp = value;
                OnPropertyChanged(nameof(Hp));
            }
        }
    }

    private float _maxHp;
    public float MaxHp
    {
        get => _maxHp;
        set
        {
            if (_maxHp == value)
                return;

            _maxHp = value;
            OnPropertyChanged(nameof(MaxHp));
        }
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