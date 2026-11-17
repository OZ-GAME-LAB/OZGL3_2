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
    public int Version;
    public string CharacterId;
    public string CharacterName;
    public string SavedAtUtc;

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

            Items = new List<SavedItemData>(),
            EquippedRuneIds = new List<string>
            {
                "", "", ""
            },
            ProgressIds = new List<string>()
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

