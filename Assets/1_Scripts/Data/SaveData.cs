using System;
using System.Collections.Generic;

[Serializable]
public class WorldSaveData
{
    public int Version;
    public string WorldId;
    public string WorldName;
    public string SavedAtUtc;

    public string MapId;
    public List<string> ProgressIds;

    public static WorldSaveData CreateNew(string name, string mapId)
    {
        return new WorldSaveData
        {
            Version = 1,
            WorldId = Guid.NewGuid().ToString("N"),
            WorldName = name.Trim(),
            SavedAtUtc = "",
            MapId = mapId,
            ProgressIds = new List<string>()
        };
    }
}

[Serializable]
public class CharacterSaveData
{
    // TODO(저장 연결) : 기획 프리팹 교체 후 기본 스폰포인트 ID를 "start_test"에서 "start_basepoint"로 변경한다.
    public const string DefaultSpawnPointId = "start_test";

    public int Version;
    public string CharacterId;
    public string CharacterName;
    public string SavedAtUtc;
    public string SpawnPointId;

    // v1 캐릭터에는 이 목록이 없을 수 있다. 가방 변경과 같은 파일에 저장한다.
    public List<FieldTransferReceipt> FieldReceipts;
    public string FieldHistoryId;
    [NonSerialized] public bool LoadedPrimary;
    public List<SavedItemData> Items;
    public List<string> EquippedRuneIds;
    public List<string> ProgressIds;

    public static CharacterSaveData CreateNew(string name)
    {
        return new CharacterSaveData
        {
            Version = 1,
            CharacterId = Guid.NewGuid().ToString("N"),
            CharacterName = name.Trim(),
            SavedAtUtc = "",
            SpawnPointId = DefaultSpawnPointId,

            Items = new List<SavedItemData>
            {
                CreateStartingTool("eq_worn_axe", EquipmentSlot.Axe),
                CreateStartingTool("eq_worn_fishingrod", EquipmentSlot.FishingRod),
                CreateStartingTool("eq_worn_pickaxe", EquipmentSlot.PickAxe)
            },
            EquippedRuneIds = new List<string>
            {
                "", "", ""
            },
            ProgressIds = new List<string>()
        };
    }

    private static SavedItemData CreateStartingTool(string itemId, EquipmentSlot slot)
    {
        return new SavedItemData
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            ItemId = itemId,
            Count = 1,
            EquippedSlot = slot
        };
    }
}

[Serializable]
public class SavedItemData
{
    public string InstanceId;
    public string ItemId;
    public int Count;

    public EquipmentSlot EquippedSlot;
}


[Serializable]
public class FieldTransferReceipt
{
    public string Token;
    public int Count;
}
