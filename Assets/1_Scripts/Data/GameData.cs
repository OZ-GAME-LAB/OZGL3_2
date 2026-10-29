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
    public string RuneType; // 룬 종류
    public string EffectType; // 효과 종류
    public string DamageType; // 변경 피해 속성

    public float DamageMultiplier = 1f;
    public float AttackSpeedMultiplier = 1f;
    public float RangeMultiplier = 1f;
    public float ProjectileSizeMultiplier = 1f;

    public int HitCount; //변경 타격 수. 0이면 변화없고 1이상일 때 변합니다.
    public bool PreventAttackInterrupt; // 공격 중단 방지
    public string StatusProfileId; // 상태이상 설정 ID
    public float HealAttackRatio; // 공격시 회복 비율
    public string Trigger; // 발동 시점
    public string AttackPrefabPath;
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

[Serializable]
public class MonsterData : GameDataBase
{
    public string Name;
    public string Description;
    public float Hp;
    public float Atk;
    public float[] AtkCollTime;
    public float AtkDistance;
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
