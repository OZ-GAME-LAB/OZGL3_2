using UnityEngine;

public class UnitVM : ViewModelBase
{
    public virtual void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(MaxHp));
        OnPropertyChanged(nameof(CurHp));
        OnPropertyChanged(nameof(IsPoisoned));
    }

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

    protected bool _isPoisoned;
    public bool IsPoisoned
    {
        get => _isPoisoned;
        set
        {
            if (_isPoisoned != value)
            {
                _isPoisoned = value;
                OnPropertyChanged(nameof(IsPoisoned));
            }
        }
    }

    protected bool _isBossPoisoned;
    public bool IsBossPoisoned
    {
        get => _isBossPoisoned;
        set
        {
            if (_isBossPoisoned != value)
            {
                _isBossPoisoned = value;
                OnPropertyChanged(nameof(IsBossPoisoned));
            }
        }
    }

    protected bool _isBleeding;
    public bool IsBleeding
    {
        get => _isBleeding;
        set
        {
            if (_isBleeding != value)
            {
                _isBleeding = value;
                OnPropertyChanged(nameof(IsBleeding));
            }
        }
    }
}
