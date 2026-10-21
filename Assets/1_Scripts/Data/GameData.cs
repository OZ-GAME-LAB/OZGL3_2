using System;
using System.Numerics;

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

public enum MoveType
{
    None = 0,
    Fix = 1,
    Move = 2
}

[Serializable]
public class GameDataBase
{
    public string Id;
}

[Serializable]
public class ItemData : GameDataBase
{
    public string Name;
    public string ItemType;
    public string Description;
    public int MaxStack;
    public int Price;
    public int SellingPrice;
    public string IconPath;
    public string PrefabPath;
}

[Serializable]
public class EquipmentData : GameDataBase
{
    public EquipmentSlot EquipmentSlot;
    public ToolAction ToolAction;
    public float Power;
}

[Serializable]
public class RuneData : GameDataBase
{
    public string EffectId;
    public Trigger Trigger;
    public int EffectValue;
    public int DurationSec;
    public float ProcChance;
}

[Serializable]
public class EffectData : GameDataBase
{
    public EffectType EffectType;
    public string Target;
    public ValueUnit ValueUnit;
    public string Description;
}

[Serializable]
public class CraftingRecipeData : GameDataBase
{
    public string Item_Id;
    public string[] RequiredMaterialId;
    public int[] RequiredMaterialQuantity;
}

[System.Serializable]
public class ItemModel
{
    public string InstanceId;
    public string ItemId;
    public int CurrentStackCount;
}

[Serializable]
public class FieldObjectData : GameDataBase
{
    public string Name;
    public string Description;
    public string InteractionType;
    public string RequiredToolAction;
    public int Tier;
    public string DropGroupId;
    public string PrefabPath;
}

[Serializable]
public class DropGroupData : GameDataBase
{
    public string[] ItemId;
    public int MinQuantity;
    public int MaxQuantity;
    public float DropChance;
}

public class MonsterData : GameDataBase
{
    public string Name;
    public string Description;
    public float Hp;
    public float Atk;
    public float PhysicalResistance;
    public float FireResistance;
    public float IceResistance;
    public float ElectricalResistance;
    public float PoisonResistance;
    public bool BleedAble;
    public string[] HaveSkillIdList;
    public MoveType MonsterType;
    public string MonsterAddressableId;
}

[Serializable]
public class MonsterPatrolData : GameDataBase
{
    public string[] PatrolSpotIdList;
}

[Serializable]
public class MonsterPatrolSpotData : GameDataBase
{
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public Vector3 Position => new Vector3(PositionX, PositionY, PositionZ);
}