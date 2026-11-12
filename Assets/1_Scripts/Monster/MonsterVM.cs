
public class MonsterVM : UnitVM
{
    public bool BleedAble { get; set; }
    public override bool CanReceiveBleed => BleedAble;

    public override void InvokeOnceOnInit()
    {
        base.InvokeOnceOnInit();
    }

    public void InitMonsterStatus(string id)
    {
        
    }
}
