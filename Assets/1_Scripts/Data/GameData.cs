using System;

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