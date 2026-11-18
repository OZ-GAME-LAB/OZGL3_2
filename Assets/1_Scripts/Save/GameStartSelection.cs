public static class GameStartSelection
{
    public static bool HasSelection { get; private set; }
    public static bool IsHost { get; private set; }

    public static string Address { get; private set; }
    public static ushort Port { get; private set; }

    public static WorldSaveData World { get; private set; }
    public static CharacterSaveData Character { get; private set; }

    public static void SetHost(
        ushort port,
        WorldSaveData world,
        CharacterSaveData character)
    {
        Clear();

        IsHost = true;
        Port = port;
        World = world;
        Character = character;
        HasSelection = world != null && character != null;
    }

    public static void SetClient(
        string address,
        ushort port,
        CharacterSaveData character)
    {
        Clear();

        IsHost = false;
        Address = address;
        Port = port;
        Character = character;
        HasSelection = character != null;
    }

    public static void Clear()
    {
        HasSelection = false;
        IsHost = false;

        Address = null;
        Port = 0;

        World = null;
        Character = null;
    }
}