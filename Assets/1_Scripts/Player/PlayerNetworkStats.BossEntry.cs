using Unity.Netcode;

public partial class PlayerNetworkStats
{
    private readonly NetworkVariable<bool> _bossEntryWaiting = new(false);
    public bool IsBossEntryWaiting => _bossEntryWaiting.Value;

    public void SetBossEntryWaitingOnServer(bool waiting)
    {
        if (IsServer && IsSpawned) _bossEntryWaiting.Value = waiting;
    }
}
