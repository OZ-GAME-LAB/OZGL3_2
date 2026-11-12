
public class MonsterVM : UnitVM
{
    public override void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(IsBleeding));
    }

    public void InitMonsterStatus(string id)
    {
        
    }
}
