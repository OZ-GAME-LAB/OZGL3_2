public class PlayerStatViewModel : ViewModelBase
{
    public virtual void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(MaxHp));
        OnPropertyChanged(nameof(CurHp));
        OnPropertyChanged(nameof(PoisonStackCount));
        OnPropertyChanged(nameof(IsPoisoned));
        OnPropertyChanged(nameof(BossPoisonStackCount));
        OnPropertyChanged(nameof(IsBossPoisoned));
        OnPropertyChanged(nameof(BleedStackCount));
        OnPropertyChanged(nameof(IsBleeding));
        OnPropertyChanged(nameof(MoveSpeed));
        OnPropertyChanged(nameof(JumpForce));
        OnPropertyChanged(nameof(AttackPower));
    }

    public float PhysicalResistance { get; set; }
    public float FireResistance { get; set; }
    public float IceResistance { get; set; }
    public float ElectricalResistance { get; set; }
    public float PoisonResistance { get; set; }

    protected float _maxHp;
    public float MaxHp
    {
        get => _maxHp;
        set
        {
            if (_maxHp != value)
            {
                _maxHp = value;
                OnPropertyChanged(nameof(MaxHp));
            }
        }
    }

    protected float _curHp;
    public float CurHp
    {
        get => _curHp;
        set
        {
            if (_curHp != value)
            {
                _curHp = value;
                OnPropertyChanged(nameof(CurHp));
            }
        }
    }

    private int _poisonStackCount;
    public int PoisonStackCount
    {
        get => _poisonStackCount;
        internal set
        {
            if (_poisonStackCount != value)
            {
                _poisonStackCount = value;
                OnPropertyChanged(nameof(PoisonStackCount));
                OnPropertyChanged(nameof(IsPoisoned));
            }
        }
    }

    public bool IsPoisoned => PoisonStackCount > 0;

    private int _bossPoisonStackCount;
    public int BossPoisonStackCount
    {
        get => _bossPoisonStackCount;
        internal set
        {
            if (_bossPoisonStackCount != value)
            {
                _bossPoisonStackCount = value;
                OnPropertyChanged(nameof(BossPoisonStackCount));
                OnPropertyChanged(nameof(IsBossPoisoned));
            }
        }
    }

    public bool IsBossPoisoned => BossPoisonStackCount > 0;

    private int _bleedStackCount;
    public int BleedStackCount
    {
        get => _bleedStackCount;
        internal set
        {
            if (_bleedStackCount != value)
            {
                _bleedStackCount = value;
                OnPropertyChanged(nameof(BleedStackCount));
                OnPropertyChanged(nameof(IsBleeding));
            }
        }
    }

    public bool IsBleeding => BleedStackCount > 0;
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
