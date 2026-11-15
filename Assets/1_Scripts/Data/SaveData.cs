using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int Version;
    public string SavedAtUtc;

    public List<SavedItemData> Items;
    public List<string> EquippedRuneIds;
    public List<string> ProgressIds;

    public static SaveData CreatNew()
    {
        return new SaveData
        {
            Version = 1,
            SavedAtUtc = "",
            Items = new List<SavedItemData>(),
            EquippedRuneIds = new List<string>
            {
                "","",""
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

    // None이면 가방, 그 외에는 해당 장비 슬롯.
    public EquipmentSlot EquippedSlot;
}
