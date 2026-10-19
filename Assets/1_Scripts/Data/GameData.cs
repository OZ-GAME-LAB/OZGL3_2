using System;

public enum ItemType
{
    None = 0,
    EquipmentItem = 1,
    Consumable = 2,
    Material = 3,
    Rune = 4
}

public enum EquipmentSlot
{
    None =0,
    Axe = 1,
    FishingRod = 2,
    PickAxe = 3
}

public enum ToolAction
{
    None = 1,
    Logging = 2,
    Fishing = 3,
    Mining = 4
}

public enum EffectType
{
    None = 0,
    InstantHeal = 1,
    StatusDamage = 2
}

public enum Target
{
    None = 0,
    Self = 1,
    HitEnemy = 2
}

public enum ValueUnit
{
    None = 0,
    HP = 1,
    HPPerSec = 2
}

public enum Trigger
{
    None = 0,
    OnHit = 1
}

[Serializable]
public class GameDataBase
{
    public string Id;
}
[Serializable]
public class InteractionGatheringData : GameDataBase
{
    public float GatheringTime;
    public string[] DropItemList;
    public int MinDropCount;
    public int MaxDropCount;
    public string MeshAddress;
}