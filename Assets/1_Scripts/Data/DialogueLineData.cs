using System;

[Serializable]
public class DialogueLineData : GameDataBase
{
    public string DialogueId;
    public int Order;
    public string SpeakerName;
    public string Text;
    public string NpcSpriteAddress;
}
