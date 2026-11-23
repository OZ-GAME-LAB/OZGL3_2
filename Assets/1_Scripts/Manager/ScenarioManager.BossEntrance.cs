using Unity.Netcode;

public sealed partial class ScenarioManager
{
    public void RequestBossPreparation(uint request, bool prepare)
    {
        if (IsSpawned && IsClient) RequestBossPreparationServerRpc(request, prepare);
    }

    public void AcknowledgeBossPreparation(uint request, bool success)
    {
        if (IsSpawned && IsClient) AcknowledgeBossPreparationServerRpc(request, success);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBossPreparationServerRpc(uint request, bool prepare, ServerRpcParams rpc = default)
    {
        if (IsReady && BossEntrance.Active != null)
            BossEntrance.Active.RequestOnServer(rpc.Receive.SenderClientId, request, prepare);
        else SendBossPreparationOnServer(rpc.Receive.SenderClientId, request, 0, 0, true, "보스 입구가 준비되지 않았습니다.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void AcknowledgeBossPreparationServerRpc(uint request, bool success, ServerRpcParams rpc = default)
    {
        BossEntrance.Active?.AcknowledgeOnServer(rpc.Receive.SenderClientId, request, success);
    }

    public void SendBossPreparationOnServer(ulong id, uint request, int accepted, int total, bool ended, string message)
    {
        if (!IsServer || !IsSpawned || !NetworkManager.ConnectedClients.ContainsKey(id)) return;
        ReceiveBossPreparationClientRpc(request, accepted, total, ended, message,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } });
    }

    [ClientRpc]
    private void ReceiveBossPreparationClientRpc(uint request, int accepted, int total, bool ended, string message,
        ClientRpcParams rpc = default)
    {
        if (BossEntrance.Active != null) BossEntrance.Active.ReceivePreparation(request, accepted, total, ended, message);
        else if (!ended) RequestBossPreparation(request, false);
    }
}
