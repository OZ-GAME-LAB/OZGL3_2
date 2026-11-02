
public class MonsterVM : ViewModelBase
{
    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(MaxHp));
        OnPropertyChanged(nameof(CurHp));
    }

    private float _maxHp;
    public float MaxHp
    {
        get => _maxHp;
        set
        {
            if(_maxHp != value)
            {
                _maxHp = value;
                OnPropertyChanged(nameof(MaxHp));
            }
        }
    }

    private float _curHp;
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

    public void InitMonsterStatus(string id)
    {
        
    }
}
